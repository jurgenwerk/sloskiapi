using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SloSkiApi.Models
{
    public class Forecast
    {
        public int ForecastId { get; set; }

        public Weather TodayMorning { get; set; }
        public Weather TodayAfternoon { get; set; }
        public Weather TomorrowMorning { get; set; }
        public Weather TomorrowAfternoon { get; set; }
        //public Weather DayAfterTomorrowMorning { get; set; }
        //public Weather DayAfterTomorrowAfternoon { get; set; }
    }
}