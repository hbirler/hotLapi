using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Net;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.Bot.Connector;

namespace Microsoft.Bot.Sample.LuisBot
{
    // For more information about this template visit http://aka.ms/azurebots-csharp-luis
    [Serializable]
    public class BasicLuisDialog : LuisDialog<object>
    {
        private const string date = "date";
        private const string money = "money";
        private List<string> keywords = new List<string>();
        public string image = "";
        public string arrivalDate = "";
        public bool arrDateGiven = false;
        public string departureDate = "";
        public bool depDateGiven = false;

        private async Task<string> GetResponseAsync(string json, IDialogContext context)
        {
            string response;
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://hotlapi.azurewebsites.net/api/Hotel");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";




            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                await streamWriter.WriteAsync(json);
            }


            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();



            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                response = await streamReader.ReadToEndAsync();
            }
            return response;
        }
        private async Task WriteHotelEntry(MessagesController.Hotel h, IDialogContext context)
        {
            var resultMessage = context.MakeMessage();
            resultMessage.Attachments = new List<Attachment>();
            HeroCard heroCard = new HeroCard()
            {
                Title = h.name,
                Subtitle = $" User review: {h.star}. \nPrice: {h.price} Euro/Day",
                Images = new List<CardImage>()
                        {
                            new CardImage() { Url = h.imgurl }
                        },
                Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = "Go To Website",
                                Type = ActionTypes.OpenUrl,
                                Value = h.url
                            }
                        }
            };
            resultMessage.Attachments.Add(heroCard.ToAttachment());
            await context.PostAsync(resultMessage);
        }

        public struct Doc
        {
            public List<string> keyPhrases;
        }
        public struct Docs
        {
            public List<Doc> documents;
        }

        private void SetKeywords(string text)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://westeurope.api.cognitive.microsoft.com/text/analytics/v2.0/keyphrases");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", "3bf9297b9a4f436d9eec7e7273a1fce0");

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = "{\"documents\": [ { \"language\": \"en\", \"id\": \"1\", \"text\": \"" + text + "\"}]}";

                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();

                Docs data = JsonConvert.DeserializeObject<Docs>(result);
                foreach (var keyword in data.documents[0].keyPhrases)
                {
                    keywords.Add(keyword);
                }
            }
        }

        public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(ConfigurationManager.AppSettings["LuisAppId"], ConfigurationManager.AppSettings["LuisAPIKey"])))
        {
        }
        public BasicLuisDialog(string image) : base(new LuisService(new LuisModelAttribute(ConfigurationManager.AppSettings["LuisAppId"], ConfigurationManager.AppSettings["LuisAPIKey"])))
        {
            this.image = image;
        }

        public async void PrintDebug(string m, IDialogContext context)
        {
            await context.PostAsync(m);
        }

        [LuisIntent("None")]
        public async Task NoneIntent(IDialogContext context, LuisResult result)
        {
            SetKeywords(result.Query);
            if (!keywords.Any())
            {
                await context.PostAsync("Sorry couldn't understand that. Could you repeat it?");
            }
            else
            {
                MessagesController.ReqJson input = new MessagesController.ReqJson();


                input.image = this.image;
                input.keywords = keywords;
                input.maxoutputsize = 3;


                string request = JsonConvert.SerializeObject(input);


                string respJS = await GetResponseAsync(request, context);


                MessagesController.RespJson respObj = JsonConvert.DeserializeObject<MessagesController.RespJson>(respJS);


                await context.PostAsync($"These are top {respObj.hotels.GetLength(0)} suitable hotels for you:");
                foreach (MessagesController.Hotel h in respObj.hotels)
                    await WriteHotelEntry(h, context);
                if (!arrDateGiven)
                    await context.PostAsync("When do you want to begin your trip?");
                else if (!depDateGiven)
                    await context.PostAsync("When do you want to end your trip?");
                else
                    await context.PostAsync("If you want to change the start date of your trip just enter the new one." +
                                                    "Additionally you can search for another trip with an image or information.");

                context.Wait(MessageReceived);
            }
        }

        [LuisIntent("dategiving")]
        public async Task Dategiving(IDialogContext context, LuisResult result)
        {
            EntityRecommendation dateEntityRecommendation;
            if (result.TryFindEntity(date, out dateEntityRecommendation))
            {
                if (!arrDateGiven)
                {
                    arrivalDate = dateEntityRecommendation.Entity;
                    arrDateGiven = true;
                    await context.PostAsync("When do you want to end your trip?");
                }
                else
                {
                    if (!depDateGiven)
                    {
                        if (!keywords.Any() && image == "")
                        {
                            await context.PostAsync("Please describe your dream trip or load an image of it.");
                        }
                        else
                        {
                            departureDate = dateEntityRecommendation.Entity;
                            depDateGiven = true;
                            MessagesController.ReqJson input = new MessagesController.ReqJson();
                            input.image = this.image;
                            input.arrivalDate = arrivalDate;
                            input.departureDate = departureDate;
                            input.keywords = keywords;
                            input.maxoutputsize = 5;
                            string request = JsonConvert.SerializeObject(input);
                            string respJS = await GetResponseAsync(request, context);
                            MessagesController.RespJson respObj = JsonConvert.DeserializeObject<MessagesController.RespJson>(respJS);
                            await context.PostAsync($"These are top {respObj.hotels.GetLength(0)} suitable hotels for you:");
                            foreach (MessagesController.Hotel h in respObj.hotels)
                                await WriteHotelEntry(h, context);
                            await context.PostAsync("If you want to change the start date of your trip just enter the new one." +
                                "Additionally you can search for another trip with an image or information.");
                            context.Wait(MessageReceived);
                        }
                    }
                    else
                    {
                        depDateGiven = false;
                        arrivalDate = dateEntityRecommendation.Entity;
                        departureDate = "";
                        await context.PostAsync("When do you want to end your trip?");
                    }
                }

            }
            else
            {
                await context.PostAsync("Sorry couldn't understand that. Could you repeat it?");
            }
            context.Wait(MessageReceived);
        }

        [LuisIntent("re_set")]
        public async Task Reset(IDialogContext context, LuisResult result)
        {
            keywords.Clear();
            arrivalDate = "";
            arrDateGiven = false;
            departureDate = "";
            depDateGiven = false;
            image = "";
            await context.PostAsync("Please describe your dream trip or load an image of it."); //
            context.Wait(MessageReceived);
        }
    }
}