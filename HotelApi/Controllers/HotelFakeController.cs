using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace HotelApi.Controllers
{
    public class HotelFakeController : ApiController
    {
        public struct PostInput
        {
            public string[] Keywords { get; set; }
            public string Image { get; set; }
            public string ArrivalDate { get; set; }
            public string DepartureDate { get; set; }
        }

        public async Task<IHttpActionResult> Post([FromBody]PostInput data)
        {
            return Ok(new {
                loc = new { lng = 7.613812387, lat = 51.9540816226 },
                hotels = new[] {
                        new {
                            name ="Parkhotel Hohenfeld Münster",
                            imgurl = "https://assets2.hotel.check24.de/size=0x450/nfc=404/source=aHR0cDovL2FmZi5ic3RhdGljLmNvbS9pbWFnZXMvaG90ZWwvbWF4NjAwLzk2My85NjM2NDc2LmpwZw==!80d430/picture.jpg",
                            url = "essek.com",
                            star = 4,
                            price = 98
                        }
                    }
            });
        }
    }
}