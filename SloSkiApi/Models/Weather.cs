using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SloSkiApi.Models
{
    public class Weather
    {
        public enum WeatherState
        {
            Snowy = 0,
            Rainy = 1,
            Cloudy = 2,
            PartlyCloudy = 3,
            Sunny = 4,
            Foggy = 5,
            Windy = 6,

        };

        public double Temperature { get; set; }
        public String WindSpeed { get; set; }
        public WeatherState Description { get; set; }
        public string DescriptionString { get; set; }
        public String Time { get; set; }
        public string Cloudiness { get; set; }
    }
}