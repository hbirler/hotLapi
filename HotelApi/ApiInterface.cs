using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace HotelApi
{
    public struct PostInput
    {
        public string[] Keywords { get; set; }
        public string Image { get; set; }
        public string ArrivalDate { get; set; }
        public string DepartureDate { get; set; }
        public int MaxOutputSize { get; set; }
    }
    public class Coordinate
    {
        public double lat;
        public double lng;
    }

    public struct Hotel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string ImgUrl { get; set; }
        public int Star { get; set; }
        public double Price { get; set; }
    }

    public static class ApiInterface
    {
        public static HttpClient client = new HttpClient();
        public static string GetCity(string[] keywords)
        {
            try
            {
                string connStr = ConfigurationManager.AppSettings["connStr"];
                using (SqlConnection sqlConnection1 = new SqlConnection(connStr))
                {
                    SqlCommand cmd = new SqlCommand();
                    SqlDataReader reader;
                    
                    cmd.CommandText = $@"
with counts as (select city, count(*) as co
from hot.keygram k
where {string.Join(" or ", Enumerable.Range(0, keywords.Length).Select(ind => $"k.keyword like @word{ind}"))}
group by k.city)

select top 1 city, sum(occurences) as occ
from hot.keygram
where ({string.Join(" or ", Enumerable.Range(0, keywords.Length).Select(ind => $"keyword like @word{ind}"))})
    and city in (select distinct city
                from counts
                where co = (select max(co) from counts))
group by city
order by occ desc";
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = sqlConnection1;
                    cmd.CommandTimeout = 0;

                    foreach (int i in Enumerable.Range(0, keywords.Length))
                    {
                        cmd.Parameters.Add($"word{i}", SqlDbType.VarChar);
                        cmd.Parameters[$"word{i}"].Value = keywords[i] + "%";
                    }

                    sqlConnection1.Open();

                    reader = cmd.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        return reader["city"].ToString();
                    }
                    else
                    {
                        return "";
                    }
                }
            }
            catch (Exception e)
            {
                return "";
            }
        }

        

        public static Coordinate GetLocation(string loc)
        {
            if (loc == "")
                return null;
            string param = HttpUtility.UrlEncode(loc.Replace(" ", "+"));
            var request = WebRequest.Create($"http://maps.googleapis.com/maps/api/geocode/json?address={param}");
            request.ContentType = "application/json; charset=utf-8";
            var response = (HttpWebResponse)request.GetResponse();

            string text;

            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                text = sr.ReadToEnd();
            }

            dynamic data = JObject.Parse(text);
            dynamic locres = data.results[0].geometry.location;
            return new Coordinate() { lat = locres.lat, lng = locres.lng };
        }

        public class GoogleVisionResult
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public bool HasCoordinates { get; set; } = false;
            public string[] descriptions = new string[0];
        }

        public static GoogleVisionResult GoogleVision(string base64image)
        {
            if (base64image == "")
                return new GoogleVisionResult();
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://vision.googleapis.com/v1/images:annotate?key=AIzaSyBdN2xmcmCIcp2JC7Zdc_aVBr-XVX83seU");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = $@"{{
  ""requests"": [
    {{
      ""image"": {{
        ""content"" : ""{base64image}""
      }},
      ""features"": [
        {{
          ""type"": ""LANDMARK_DETECTION"",
          ""maxResults"": 1
        }},
        {{
          ""type"": ""WEB_DETECTION"",
          ""maxResults"": 2
        }}
      ]
    }}
  ]
}}";
                    streamWriter.Write(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    JObject data = JObject.Parse(result);

                    GoogleVisionResult gres = new GoogleVisionResult();
                    gres.descriptions = new string[0];
                    gres.HasCoordinates = false;

                    try
                    {
                        var essek = data["responses"][0]["landmarkAnnotations"][0]["locations"][0]["latLng"];
                        gres.Latitude = Convert.ToDouble(essek["latitude"]);
                        gres.Longitude = Convert.ToDouble(essek["longitude"]);
                        gres.HasCoordinates = true;
                    }
                    catch
                    { }

                    try
                    {
                        var essek = data["responses"][0]["webDetection"]["webEntities"];
                        gres.descriptions = essek.Select(x => x["description"].ToString()).ToArray();
                    }
                    catch { }
                    
                    return gres;
                }
            }
            catch
            {
                return null;
            }
        }

        public static string[] MicrosoftKeywords(string text)
        {
            if (text == "")
                return new string[0];
            try
            {
                string uribase = "https://westeurope.api.cognitive.microsoft.com/text/analytics/v2.0/keyPhrases";
                string uriparam = "";
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uribase + uriparam);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", "2b75405891744bfdae038521c17304db");

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = $@"{{
    ""documents"": [
        {{
            ""language"": ""en"",
            ""id"": ""1"",
            ""text"": ""{text}""
        }}
    ]
}}";
                    streamWriter.Write(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    JObject data = JObject.Parse(result);

                    return data["documents"][0]["keyPhrases"].Select(x => x.ToString()).ToArray();
                }
            }
            catch (Exception e)
            {
                return new string[0];
            }
        }

        public static string MicrosoftVision(string base64image)
        {
            if (base64image == "")
                return "";
            try
            {
                string uribase = "https://westeurope.api.cognitive.microsoft.com/vision/v1.0/analyze";
                string uriparam = "?visualFeatures=Categories,Description,Color&language=en";
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uribase + uriparam);
                httpWebRequest.ContentType = "application/octet-stream";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", "9ee5f3bfa16a4a859232a454e737232d");

                using (var stream = httpWebRequest.GetRequestStream())
                {
                    byte[] data = Convert.FromBase64String(base64image);
                    stream.Write(data, 0, data.Length);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    JObject data = JObject.Parse(result);

                    return data["description"]["captions"][0]["text"].ToString();
                }
            }
            catch (Exception e)
            {
                return "";
            }
        }

        public static (Coordinate[], string[]) GetHotelLocations(PostInput data)
        {
            try
            {
                if (data.Keywords == null)
                    data.Keywords = new string[0];
                
                string[] popo = new string[0];
                string[] popo2 = new string[0];
                GoogleVisionResult res = new GoogleVisionResult();

                if (data.Image != null && data.Image != "")
                {
                    string desc = ApiInterface.MicrosoftVision(data.Image);
                    //string[] popo = ApiInterface.MicrosoftKeywords(desc);
                    res = ApiInterface.GoogleVision(data.Image);

                    popo2 = res.descriptions.SelectMany(x => ApiInterface.MicrosoftKeywords(x)).ToArray();
                }



                string[] safakeys = data.Keywords.Select(x => x.ToLower().Trim()).ToArray();
                string[] allkeys = data.Keywords.Concat(popo).Concat(popo2).Select(x => x.ToLower().Trim()).ToArray();
                string[] imgkeys = popo.Concat(popo2).Select(x => x.ToLower().Trim()).ToArray();


                string[] cities = new string[] { ApiInterface.GetCity(allkeys), ApiInterface.GetCity(safakeys), ApiInterface.GetCity(imgkeys) };

                var locations = new List<Coordinate>();

                if (res.HasCoordinates)
                {
                    locations.Add(new Coordinate() { lat = res.Latitude, lng = res.Longitude });
                }

                locations.AddRange(cities.Where(x => x != "").Select(x => ApiInterface.GetLocation(x)));

                return (locations.ToArray(), cities);
            }
            catch
            {
                return (new Coordinate[0], new string[0]);
            }
        }

        public static string UrlFriend(object str)
        {
            return HttpUtility.UrlEncode(str.ToString().Replace(' ', '+'));
        }

        public static Hotel[] Check24(double latitude, double longitude, DateTime arrivalDate, DateTime departureDate, float radius = 10, string roomConfiguration = "[A]")
        {
            
            string arrstr = arrivalDate.ToString("yyyy-MM-dd");
            string depstr = departureDate.ToString("yyyy-MM-dd");
            try
            {
                int resultId = -1;
                while (true)
                {
                    string uribase = "https://api.hotel.check24.de/hackatum/hotels/searches.json";
                    string uriparam = $"?latitude={latitude}&longitude={longitude}&radius={radius}&arrival_date={arrstr}&departure_date={depstr}&room_configuration={roomConfiguration}";
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(uribase + uriparam);
                    //httpWebRequest.ContentType = "application/octet-stream";
                    httpWebRequest.Method = "POST";

                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        JObject data = JObject.Parse(result);
                        string state = data["search"]["status_detailed"]["state"].ToString();
                        resultId = Convert.ToInt32(data["search"]["id"].ToString());

                        if (state == "finished")
                            break;
                    }
                }

                if (resultId == -1)
                    throw new Exception("sheeet");
                {
                    string uribase = $"https://api.hotel.check24.de/hackatum/hotels/searches/{resultId}/results.json";

                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(uribase);
                    httpWebRequest.Method = "POST";

                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        JObject data = JObject.Parse(result);
                        return data["search"]["results"].Select(x => new Hotel()
                        {
                            Id = Convert.ToInt32(x["hotel_id"].ToString()),
                            Name = x["name"].ToString(),
                            Url = $"https://hotel.check24.de/HotL/{UrlFriend(x["city"])}-{x["city_id"]}/{UrlFriend(x["name"])}-{x["hotel_id"]}/{arrstr}/{depstr}/{roomConfiguration}/hotel.html",
                            ImgUrl = x["image_url"].ToString(),
                            Price = Convert.ToDouble(x["price"].ToString()),
                            Star = (int)Math.Round(Convert.ToDouble(x["rating_average"].ToString()))
                        }).ToArray();
                    }
                }

            }
            catch (Exception e)
            {
                return new Hotel[0];
            }
        }
    }
}