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

namespace HotelApi
{
    public class ApiInterface
    {
        public static HttpClient client = new HttpClient();
        public static async Task<string> GetCity(string[] keywords)
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

                    await sqlConnection1.OpenAsync();

                    reader = cmd.ExecuteReader();

                    if (reader.HasRows)
                    {
                        await reader.ReadAsync();
                        return reader["city"].ToString();
                    }
                    else
                    {
                        return "Munich";
                    }
                }
            }
            catch (Exception e)
            {
                return "Munich";
            }
        }

        public struct Coordinates
        {
            public double lat;
            public double lng;
        }

        public static async Task<Coordinates> GetLocation(string loc)
        {
            string param = HttpUtility.UrlEncode(loc.Replace(" ", "+"));
            var request = WebRequest.Create($"https://maps.googleapis.com/maps/api/geocode/json?address={param}");
            request.ContentType = "application/json; charset=utf-8";
            var response = (HttpWebResponse)await request.GetResponseAsync();

            string text;

            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                text = await sr.ReadToEndAsync();
            }

            dynamic data = JObject.Parse(text);
            dynamic locres = data.results[0].geometry.location;
            return new Coordinates() { lat = locres.lat, lng = locres.lng };
        }

        class GoogleVisionResult
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public bool HasCoordinates { get; set; } = false;
            public string[] descriptions = new string[0];
        }

        public static async Task<GoogleVisionResult> GoogleVision(string base64image)
        {
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
                    await streamWriter.WriteAsync(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
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

        public static async Task<string[]> MicrosoftKeywords(string text)
        {
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
                    await streamWriter.WriteAsync(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    JObject data = JObject.Parse(result);

                    return data["documents"][0]["keyPhrases"].Select(x => x.ToString()).ToArray();
                }
            }
            catch (Exception e)
            {
                return new string[0];
            }
        }

        public static async Task<string> MicrosoftVision(string base64image)
        {
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
                    await stream.WriteAsync(data, 0, data.Length);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    JObject data = JObject.Parse(result);

                    return data["description"]["captions"][0]["text"].ToString();
                }
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }
}