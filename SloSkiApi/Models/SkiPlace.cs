using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace SloSkiApi.Models
{
    public class SkiPlace
    {
        public String Name { get; set; }
        public int Height { get; set; }
        public double CurrentSnowLevel { get; set; }
        public Weather CurrentWeather { get; set; }
        public Forecast Forecast { get; set; }
        public string UpdatedAt { get; set; }
        public List<String> CamsUrlList { get; set; }
        //public String Notification { get; set; } //nastavi, če hočeš kaj sporočiti uporabniku v popup oknu
    }
}