using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace HotelApi.Controllers
{
    

    public class HotelController : ApiController
    {

        public IHttpActionResult Get()
        {
            return Ok(new PostInput());
        }

        // POST api/<controller>
        public IHttpActionResult Post([FromBody]PostInput data)
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");
            if (data.Keywords == null)
                data.Keywords = new string[0];
            List<string> addkeywords = new List<string>();
            foreach (string key in data.Keywords)
            {
                string[] ks = key.Split(' ');
                if (ks.Length == 2)
                {
                    addkeywords.AddRange(ks);
                }
                if (ks.Length > 2)
                {
                    addkeywords.AddRange(ks.Zip(ks.Skip(1), (x, y) => x + " " + y));
                }
            }

            data.Keywords = data.Keywords.Concat(addkeywords).ToArray();

            (Coordinate[] coordinates, string[] cities) = ApiInterface.GetHotelLocations(data);

            var parser = new Chronic.Parser();

            if (data.ArrivalDate == null || data.ArrivalDate == "")
                data.ArrivalDate = "today";
            if (data.DepartureDate == null || data.DepartureDate == "")
                data.DepartureDate = "next week";
            if (data.Image == null)
                data.Image = "";

            var opt = new Chronic.Options();
            opt.EndianPrecedence = Chronic.EndianPrecedence.Little;

            DateTime arrivalDate;
            DateTime departureDate;

            if (data.ArrivalDate != null)
                data.ArrivalDate.Replace('.', '/');
            if (data.DepartureDate != null)
                data.DepartureDate.Replace('.', '/');

            try
            {
                arrivalDate = DateTime.Parse(data.ArrivalDate, null);
                departureDate = DateTime.Parse(data.DepartureDate, null);
            }
            catch
            {
                arrivalDate = parser.Parse(data.ArrivalDate, opt).Start.Value;
                departureDate = parser.Parse(data.DepartureDate, opt).Start.Value;
            }

            Hotel[] hotels = coordinates.SelectMany(x => ApiInterface.Check24(x.lat, x.lng, arrivalDate, departureDate)).ToArray();

            hotels = hotels.Where((x, i) => !hotels.Take(i).Any(y => y.Id == x.Id)).ToArray();

            return Ok(new { Hotels = hotels.Take(data.MaxOutputSize), Coordinates = coordinates, Cities = cities });
        }
    }
}