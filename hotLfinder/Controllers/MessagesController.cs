using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Sample.LuisBot;
using Microsoft.Bot.Builder.Dialogs;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Web;

namespace Microsoft.Bot.Sample.LuisBot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private string image;
        private bool transported;
        public struct ReqJson
        {
            public List<string> keywords;
            public string image;
            public string arrivalDate;
            public string departureDate;
            public int maxoutputsize;
        }
        public struct RespJson
        {

            public Location loc;
            public Hotel[] hotels;
        }
        public struct Location
        {
            public float lng;
            public float lat;
        }
        public struct Hotel
        {
            public string name;
            public string url;
            public string imgurl;
            public int star;
            public string price;
        }

        private async Task<string> GetResponseAsync(string json)
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
        private async Task WriteHotelEntry(Hotel h, [FromBody]Activity activity, ConnectorClient connector)
        {
            HeroCard heroCard = new HeroCard()
            {
                Title = h.name,
                Subtitle = $"User Reviews {h.star}. \nPrice: {h.price} Euro/Day",
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
            Activity reply = activity.CreateReply("");
            reply.Attachments = new List<Attachment>();
            reply.Attachments.Add(heroCard.ToAttachment());
            await connector.Conversations.ReplyToActivityAsync(reply);
        }

        public static async Task<byte[]> ReadFully(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    await ms.WriteAsync(buffer, 0, read);
                }
            }
        }
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            try
            {
                if (activity.Type == ActivityTypes.Message)
                {
                    var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                    string message;
                    Attachment imageAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
                    byte[] attachmentContents;

                    if (imageAttachment != null)
                    {
                        try
                        {
                            var stream = await GetImageStream(connector, imageAttachment);

                            attachmentContents = await ReadFully(stream);

                            this.image = Convert.ToBase64String(attachmentContents);
                            ReqJson input = new ReqJson();
                            input.image = this.image;
                            input.maxoutputsize = 3;
                            transported = false;
                            string request = JsonConvert.SerializeObject(input);

                            string respJS = await GetResponseAsync(request);
                            RespJson respObj = JsonConvert.DeserializeObject<RespJson>(respJS);
                            if (respObj.hotels.Any())
                            {
                                string message3 = $"These are top {respObj.hotels.GetLength(0)} suitable hotels for you:";
                                Activity reply3 = activity.CreateReply(message3);
                                await connector.Conversations.ReplyToActivityAsync(reply3);
                                foreach (Hotel h in respObj.hotels)
                                    await WriteHotelEntry(h, activity, connector);
                                string message2 = "When are you planning to begin your trip?";
                                Activity reply2 = activity.CreateReply(message2);
                                await connector.Conversations.ReplyToActivityAsync(reply2);
                            }
                            else
                            {
                                string message5 = "I couldn't find any matching hotel. Please try again.";
                                Activity reply = activity.CreateReply(message5);
                                await connector.Conversations.ReplyToActivityAsync(reply);
                            }

                        }
                        catch (Exception e)
                        {
                            message = "Oops! Something went wrong. Try again later" + e.ToString();
                            //if (e is ClientException && (e as ClientException).Error.Message.ToLowerInvariant().Contains("access denied"))
                            //{
                            //    message += " (access denied - hint: check your APIKEY at web.config).";
                            //}

                            Trace.TraceError(e.ToString());
                            Activity reply = activity.CreateReply(message);
                            await connector.Conversations.ReplyToActivityAsync(reply);
                        }
                    }
                    else
                    {
                        if (transported)
                        {
                            await Conversation.SendAsync(activity, () => new BasicLuisDialog());
                        }
                        else
                        {
                            await Conversation.SendAsync(activity, () => new BasicLuisDialog(image));
                            transported = true;
                        }
                    }
                }
                else
                {
                    await this.HandleSystemMessage(activity);
                }

                var response = this.Request.CreateResponse(HttpStatusCode.OK);
                return response;
            }
            catch (Exception e)
            {
                e.ToString();
                var response = this.Request.CreateResponse(HttpStatusCode.OK);
                return response;
            }
        }

        private static async Task<Stream> GetImageStream(ConnectorClient connector, Attachment imageAttachment)
        {
            using (var httpClient = new HttpClient())
            {
                // The Skype attachment URLs are secured by JwtToken,
                // you should set the JwtToken of your bot as the authorization header for the GET request your bot initiates to fetch the image.
                // https://github.com/Microsoft/BotBuilder/issues/662
                var uri = new Uri(imageAttachment.ContentUrl);
                if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                }

                return await httpClient.GetStreamAsync(uri);
            }
        }

        /// <summary>
        /// Gets the JwT token of the bot. 
        /// </summary>
        /// <param name="connector"></param>
        /// <returns>JwT token of the bot</returns>
        private static async Task<string> GetTokenAsync(ConnectorClient connector)
        {
            var credentials = connector.Credentials as MicrosoftAppCredentials;
            if (credentials != null)
            {
                return await credentials.GetTokenAsync();
            }

            return null;
        }

        /// <summary>
        /// Handles the system activity.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <returns>Activity</returns>
        private async Task<Activity> HandleSystemMessage(Activity activity)
        {
            switch (activity.Type)
            {
                case ActivityTypes.DeleteUserData:
                    // Implement user deletion here
                    // If we handle user deletion, return a real message
                    break;
                case ActivityTypes.ConversationUpdate:
                    // Greet the user the first time the bot is added to a conversation.
                    if (activity.MembersAdded.Any(m => m.Id == activity.Recipient.Id))
                    {
                        var connector = new ConnectorClient(new Uri(activity.ServiceUrl));

                        var response = activity.CreateReply();
                        response.Text = "Hi! Welcome to the hotL bot. Give me a photo or information of the place you want to go.";

                        await connector.Conversations.ReplyToActivityAsync(response);
                    }

                    break;
                case ActivityTypes.ContactRelationUpdate:
                    // Handle add/remove from contact lists
                    break;
                case ActivityTypes.Typing:
                    // Handle knowing that the user is typing
                    break;
                case ActivityTypes.Ping:
                    break;
            }

            return null;
        }
    }
}