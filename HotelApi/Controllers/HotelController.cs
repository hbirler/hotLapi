using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace HotelApi.Controllers
{
    public class HotelController : ApiController
    {
        public struct PostInput
        {
            public string[] Keywords { get; set; }
            public string Image { get; set; }
            public string ArrivalDate { get; set; }
            public string DepartureDate { get; set; }
            public int MaxOutputSize { get; set; }
        }

        // POST api/<controller>
        public async Task<IHttpActionResult> Post([FromBody]PostInput data)
        {
            string desc = await ApiInterface.MicrosoftVision(SampleImage.Fish);
            string[] popo = await ApiInterface.MicrosoftKeywords(desc);
            var res = await ApiInterface.GoogleVision(SampleImage.Fish);

            var city = await ApiInterface.GetCity(data.Keywords);
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var loc = await ApiInterface.GetLocation(city);
                    return Ok(new { City = city, Location = loc });
                }
                catch
                {

                }
            }
            return Ok(new { City = city });
        }
    }
}