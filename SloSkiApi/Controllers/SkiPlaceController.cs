using HtmlAgilityPack;
using SloSkiApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Http;
using Finisar.SQLite;
using System.Data;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Data.SqlClient;
using GoogleAnalyticsTracker;

namespace SloSkiApi.Controllers
{
    public class SkiPlaceController : ApiController
    {
        string connectionString = @"Data Source=plskwsql1.jodohost.net;Initial Catalog=matix_;Persist Security Info=True;User ID=;Password=";
        SqlConnection conn;
        SqlCommand comm;    

        // GET api/values
        public IEnumerable<string> Get()
        {
            return new string[] { };
        }

        // GET api/skiPlace/all
        public SkiPlace[] Get(string skiPlaceName)
        {
            if (skiPlaceName != "refresh")
            {
                Tracker tracker = new Tracker("UA-37669103-1", "http://skiapi.jurglic.si");

                var request = System.Web.HttpContext.Current.Request;
                tracker.Hostname = request.UserHostName;
                tracker.UserAgent = request.UserAgent;
                tracker.Language = request.UserLanguages != null ? string.Join(";", request.UserLanguages) : "";

                tracker.TrackPageView("Ski api get", "api/skiPlace/all");

            }


            conn = new SqlConnection(this.connectionString);
            conn.Open();
            comm = new SqlCommand("", conn);

            List<SkiPlace> skiPlacesData = null;

            if (skiPlaceName == "refresh")
            {
                Dictionary<string, Tuple<int, string, int>> snezniTelefonData = DataFromSnezniTelefon();
                skiPlacesData = DataFromZurnal(snezniTelefonData);
                //skiPlacesData = DataFromHribi(snezniTelefonData); //TEMPORARY DOKLER SE ZURNAL NE POBERE
            }

            DataTable dt = GetLastEntry();
            skiPlacesData = CreateObjects(dt);

            if (skiPlacesData != null)
                return skiPlacesData.ToArray();

            try
            {
                Dictionary<string, Tuple<int, string, int>> snezniTelefonData = DataFromSnezniTelefon();
                skiPlacesData = DataFromZurnal(snezniTelefonData);
            }
            catch (Exception ex)
            {
                //send mail 
                dt = GetLastEntry();
                skiPlacesData = CreateObjects(dt);
                return skiPlacesData.ToArray();
            }
            finally
            {
                conn.Close();
            }

            return skiPlacesData.ToArray();
        }

        public Dictionary<string, Tuple<int, string, int>> DataFromSnezniTelefon()
        {
            var culture = new CultureInfo("sl-SI");
            Thread.CurrentThread.CurrentUICulture = culture;
            Dictionary<string, Tuple<int, string, int>> snezniTelefonData = new Dictionary<string, Tuple<int,string, int>>();

            string url = "http://www.snezni-telefon.si/";
            WebClient client = new WebClient();
            string htmlCode = DownloadString(client, url, Encoding.GetEncoding("Windows-1250"));

            HtmlDocument document = new HtmlDocument();
            string htmlString = htmlCode;
            document.LoadHtml(htmlString);

            var tabs = document.DocumentNode.Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "underline" && d.ParentNode.Attributes["id"].Value == "data_tab_1_1").ToList();

            foreach (var tab in tabs)
            {
                snezniTelefonData = ParseSkiTab(tab.InnerHtml, snezniTelefonData);
            }

            return snezniTelefonData;
        }

        public List<SkiPlace> DataFromZurnal(Dictionary<string, Tuple<int, string, int>> snezniTelefonDict)
        {
            List<SkiPlace> skiPlaces = new List<SkiPlace>();
            List<string> urls = new List<String> 
            {
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/cerkno",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/mariborsko-pohorje/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/kanin/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/kranjska-gora/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/bukovnik/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/krvavec/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/rogla/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/vogel/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/kope/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/stari-vrh/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/crna-na-koroskem/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/bela/dole-pri-litiji/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/golte/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/javornik/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/ski-bor/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/smucisce-poseka/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/rudno/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/soriska-planina/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/sviscaki/",
                "http://vreme.zurnal24.si/prosti-cas/smucanje/slovenija/celjska-koca/"

            };


            comm.CommandText = "select max(zap_st) from SkiSlope";
            int zapSt = Convert.ToInt32(comm.ExecuteScalar());
            zapSt++;
            

            foreach (string url in urls)
            {
                skiPlaces.Add(ParseZurnal(url, snezniTelefonDict, zapSt));
            }
            return skiPlaces;
        }

        public SkiPlace ParseZurnal(string url, Dictionary<string, Tuple<int, string, int>> snezniTelefonDict, int zapSt)
        {

            WebClient client = new WebClient();
            string htmlCode = DownloadString(client, url, Encoding.UTF8);

            HtmlDocument document = new HtmlDocument();
            string htmlString = htmlCode;
            document.LoadHtml(htmlString);

            HtmlNode name = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/h1/big/text()");
            string skiName;
            if (name != null)
                skiName = name.OuterHtml.Trim();
            else skiName = "N/A";
            if (skiName == "Mariborsko Pohorje")
                skiName = "M. Pohorje";

            HtmlNode height = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/h1/big/em[1]");
            string heightS;
            if (height != null)
                heightS = height.InnerHtml.Replace("&nbsp;", "").Replace("/", "").Replace("višina", "").Trim();
            else
                heightS = "N/A";

            HtmlNode temp = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/ul/li[2]/p/big");
            string tempS;
            if (temp != null)
                tempS = temp.InnerHtml.Replace("&deg;C", "");
            else
                tempS = "N/A";

            HtmlNode weatherDesc = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/ul/li[1]/p/text()");
            string weatherDescS;
            if (weatherDesc != null)
                weatherDescS = weatherDesc.ParentNode.InnerHtml.Substring(weatherDesc.ParentNode.InnerHtml.LastIndexOf("</span>") + "</span>".Length).Trim().Replace("  ", "").Replace("  ", "");
            else
                weatherDescS = "N/A";

            weatherDescS = weatherDescS.Replace("è", "č");

            HtmlNode weatherDescIndex = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/ul/li[1]/p/span/object/param[1]");
            string weatherDescIndexS;
            if (weatherDescIndex != null)
                weatherDescIndexS = Path.GetFileNameWithoutExtension(weatherDescIndex.Attributes["value"].Value);
            else
                weatherDescIndexS = "N/A";


            HtmlNode oblacnost = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/ul/li[5]/p/big");
            string oblacnostS;
            if (oblacnost != null)
                oblacnostS = oblacnost.InnerHtml;
            else
                oblacnostS = "N/A";

            HtmlNode veter = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/ul/li[3]/p/big");
            string veterS;
            if (veter != null)
                veterS = veter.InnerHtml.Replace("km/h", "").Trim();
            else
                veterS = "N/A";

            HtmlNode refreshed = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[1]/div/blockquote/p/em");
            string refreshedS;
            if (refreshed != null)
                refreshedS = refreshed.InnerHtml.Replace("<strong>", "").Replace("</strong>", "").Trim();
            else
                refreshedS = "N/A";

            //se iz sneznega telefona vzamemo ce je kej pametnega

            int snowLevel = 0;

            foreach (KeyValuePair<String, Tuple<int, string, int>> entry in snezniTelefonDict)
            {
                if ((skiName.Split(' ').ToList()).Intersect(entry.Key.Split(' ').ToList()).Count() > 0)
                {
                    snowLevel = entry.Value.Item1;
                    if (entry.Value.Item2.Length > 4)
                    {
                        weatherDescS = entry.Value.Item2.Replace("&nbsp;", "").Trim();
                        var a = weatherDescS.ToCharArray();
                        a[0] = Char.ToUpper(a[0]);
                        weatherDescS = new string(a);
                    }

                }
            }

            //danes napoved

            HtmlNode danesDatum = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[1]/h2/small");
            string danesDatumS;
            if (danesDatum != null)
            {
                danesDatumS = danesDatum.InnerHtml.Substring(2);
                danesDatumS = danesDatumS.Substring(0, danesDatumS.Length - 4);
                var a = danesDatumS.ToCharArray();
                a[0] = Char.ToUpper(a[0]);
                danesDatumS = new string(a);
            }
            else
                danesDatumS = "N/A";


            HtmlNode danesDopoldneTemp = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[2]/ul/li[2]/p/big");
            string danesDopoldneTemps;
            if (danesDopoldneTemp != null)
                danesDopoldneTemps = danesDopoldneTemp.InnerHtml.Replace("&deg;C", "");
            else
                danesDopoldneTemps = "N/A";

            HtmlNode danesDopoldneVeter = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[2]/ul/li[2]/p/small");
            string danesDopoldneVeterS;

            if (danesDopoldneVeter != null)
                danesDopoldneVeterS = danesDopoldneVeter.InnerHtml;
            else
                danesDopoldneVeterS = "N/A";

            HtmlNode danesDopoldneweatherDescIndex = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[2]/ul/li[2]/p/span[1]/object/param[1]");
            string danesDopoldneweatherDescIndexS;
            if (danesDopoldneweatherDescIndex != null)
                danesDopoldneweatherDescIndexS = Path.GetFileNameWithoutExtension(danesDopoldneweatherDescIndex.Attributes["value"].Value);
            else
                danesDopoldneweatherDescIndexS = "3";


            HtmlNode danesPopoldneTemp = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[2]/ul/li[3]/p/big");
            string danesPopoldneTemps;
            if (danesPopoldneTemp != null)
                danesPopoldneTemps = danesPopoldneTemp.InnerHtml.Replace("&deg;C", "");
            else
                danesPopoldneTemps = "0";

            HtmlNode danesPopoldneVeter = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[2]/ul/li[3]/p/small");
            string danesPopoldneVeterS;

            if (danesPopoldneVeter != null)
                danesPopoldneVeterS = danesPopoldneVeter.InnerHtml;
            else
                danesPopoldneVeterS = "N/A";

            HtmlNode danesPopoldneweatherDescIndex = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[2]/div/div[2]/ul/li[3]/p/span[1]/object/param[1]");
            string danesPopoldneweatherDescIndexS;
            if (danesPopoldneweatherDescIndex == null)
                danesPopoldneweatherDescIndexS = Path.GetFileNameWithoutExtension(danesPopoldneweatherDescIndex.Attributes["value"].Value);
            else
                danesPopoldneweatherDescIndexS = "3";

            //jutri napoved

            HtmlNode jutriDatum = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[1]/h2/small");
            string jutriDatumS;
            if (jutriDatum != null)
            {
                jutriDatumS = jutriDatum.InnerHtml;

                jutriDatumS = jutriDatumS.Substring(0, jutriDatumS.Length - 4);
                var a = jutriDatumS.ToCharArray();
                a[0] = Char.ToUpper(a[0]);
                jutriDatumS = new string(a);
            }
            else
                jutriDatumS = "N/A";

            HtmlNode jutriDopoldneTemp = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[2]/ul/li[2]/p/big");
            string jutriDopoldneTemps;
            if (jutriDopoldneTemp != null)
                jutriDopoldneTemps = jutriDopoldneTemp.InnerHtml.Replace("&deg;C", "");
            else
                jutriDopoldneTemps = "N/A";

            HtmlNode jutriDopoldneVeter = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[2]/ul/li[2]/p/small");
            string jutriDopoldneVeterS;
            if (jutriDopoldneVeter != null)
                jutriDopoldneVeterS = jutriDopoldneVeter.InnerHtml;
            else
                jutriDopoldneVeterS = "N/A";

            HtmlNode jutriDopoldneweatherDescIndex = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[2]/ul/li[2]/p/span[1]/object/param[1]");
            string jutriDopoldneweatherDescIndexS;

            if (jutriDopoldneweatherDescIndex != null)
                jutriDopoldneweatherDescIndexS = Path.GetFileNameWithoutExtension(jutriDopoldneweatherDescIndex.Attributes["value"].Value);
            else
                jutriDopoldneweatherDescIndexS = "3";

            HtmlNode jutriPopoldneTemp = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[2]/ul/li[3]/p/big");
            string jutriPopoldneTemps;

            if (jutriPopoldneTemp != null)
                jutriPopoldneTemps = jutriPopoldneTemp.InnerHtml.Replace("&deg;C", "");
            else
                jutriPopoldneTemps = "N/A";

            HtmlNode jutriPopoldneVeter = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[2]/ul/li[3]/p/small");
            string jutriPopoldneVeterS;

            if (jutriPopoldneVeter != null)
                jutriPopoldneVeterS = jutriPopoldneVeter.InnerHtml;
            else
                jutriPopoldneVeterS = "N/A";

            HtmlNode jutriPopoldneweatherDescIndex = document.DocumentNode.SelectSingleNode(@"//*[@id=""primaryContent""]/div[3]/div/div[2]/ul/li[3]/p/span[1]/object/param[1]");
            string jutriPopoldneweatherDescIndexS;

            if (jutriPopoldneweatherDescIndex != null)
                jutriPopoldneweatherDescIndexS = Path.GetFileNameWithoutExtension(jutriPopoldneweatherDescIndex.Attributes["value"].Value);
            else
                jutriPopoldneweatherDescIndexS = "3";

            Debug.WriteLine(
                url + ": " + Environment.NewLine
                + "Ime: " + skiName + Environment.NewLine
                + "Višina: " + heightS + Environment.NewLine
                + "Temperatura: " + tempS + Environment.NewLine
                + "Sneg: " + snowLevel + Environment.NewLine
                + "Vreme: " + weatherDescS + Environment.NewLine
                + "Vreme index: " + weatherDescIndexS + Environment.NewLine
                + "Veter: " + veterS + Environment.NewLine
                + "Oblacnost: " + oblacnostS + Environment.NewLine
                + "Osvezeno: " + refreshedS + Environment.NewLine
                + "Napoved " + Environment.NewLine
                + "\t Danes (" + danesDatumS + ")" + Environment.NewLine
                + "\t\t Dopoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + danesDopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + danesDopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + danesDopoldneVeterS + Environment.NewLine
                + "\t\t Popoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + danesPopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + danesPopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + danesPopoldneVeterS + Environment.NewLine
                + "\t Jutri (" + jutriDatumS + ")" + Environment.NewLine
                + "\t\t Dopoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + jutriDopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + jutriDopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + jutriDopoldneVeterS + Environment.NewLine
                + "\t\t Popoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + jutriPopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + jutriPopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + jutriPopoldneVeterS
                );

            Debug.WriteLine(Environment.NewLine);


            SkiPlace skiPlace = new SkiPlace();

            skiPlace.Name = skiName;
            skiPlace.CurrentSnowLevel = snowLevel;
            skiPlace.Height = Convert.ToInt32(heightS.Replace("m", "").Replace(":", "").Trim());
            skiPlace.UpdatedAt = refreshedS;

            Weather weather = new Weather();
            weather.Temperature = Convert.ToInt32(tempS);
            weather.Description = (Weather.WeatherState)Convert.ToInt32(weatherDescIndexS);
            weather.DescriptionString = weatherDescS;
            weather.WindSpeed = veterS;
            weather.Cloudiness = oblacnostS.Replace("%", "").Trim();

            //še mal extra potweakamo ikonco iz opisa

            if (weatherDescS != null && weatherDescS.Length > 2)
            {
                weatherDescS = weatherDescS.Trim().ToLower();

                if (weatherDescS == "jasno" || weatherDescS == "sončno" )
                    weather.Description = 0;
                else if (weatherDescS.Contains("megl"))
                    weather.Description = (Weather.WeatherState)2;
                else if (weatherDescS == "oblačno")
                    weather.Description = (Weather.WeatherState)8;
                else if (weatherDescS.Contains("sneg") || weatherDescS.Contains("snež"))
                    weather.Description = (Weather.WeatherState)10;
                else if (weatherDescS.Contains("prete") && weatherDescS.Contains("obla"))
                    weather.Description = (Weather.WeatherState)13;
                else if (weatherDescS.Contains("prete") && weatherDescS.Contains("jasno"))
                    weather.Description = (Weather.WeatherState)4;
                else if (weatherDescS.Contains("deln") && weatherDescS.Contains("obla"))
                    weather.Description = (Weather.WeatherState)4;
                else if (weatherDescS.Contains("deln") && weatherDescS.Contains("jasn"))
                    weather.Description = (Weather.WeatherState)4;
              
            }

            Weather weatherTodayMorning = new Weather();
            weatherTodayMorning.Temperature = Convert.ToInt32(danesDopoldneTemps);
            weatherTodayMorning.Description = (Weather.WeatherState)Convert.ToInt32(danesDopoldneweatherDescIndexS);
            weatherTodayMorning.Time = danesDatumS;
            weatherTodayMorning.WindSpeed = danesDopoldneVeterS;

            Weather weatherTodayAfternoon = new Weather();
            weatherTodayAfternoon.Temperature = Convert.ToInt32(danesPopoldneTemps);
            weatherTodayAfternoon.Description = (Weather.WeatherState)Convert.ToInt32(danesPopoldneweatherDescIndexS);
            weatherTodayAfternoon.Time = danesDatumS;
            weatherTodayAfternoon.WindSpeed = danesPopoldneVeterS;

            Weather weatherTomorrowMorning = new Weather();
            weatherTomorrowMorning.Temperature = Convert.ToInt32(jutriDopoldneTemps);
            weatherTomorrowMorning.Description = (Weather.WeatherState)Convert.ToInt32(jutriDopoldneweatherDescIndexS);
            weatherTomorrowMorning.Time = jutriDatumS;
            weatherTomorrowMorning.WindSpeed = jutriDopoldneVeterS;

            Weather weatherTomorrowAfternoon = new Weather();
            weatherTomorrowAfternoon.Temperature = Convert.ToInt32(jutriPopoldneTemps);
            weatherTomorrowAfternoon.Description = (Weather.WeatherState)Convert.ToInt32(jutriPopoldneweatherDescIndexS);
            weatherTomorrowAfternoon.Time = jutriDatumS;
            weatherTomorrowAfternoon.WindSpeed = jutriPopoldneVeterS;


            Forecast f = new Forecast();

            f.TodayMorning = weatherTodayMorning;
            f.TodayAfternoon = weatherTodayAfternoon;
            f.TomorrowMorning = weatherTomorrowMorning;
            f.TomorrowAfternoon = weatherTomorrowAfternoon;

            skiPlace.CurrentWeather = weather;
            skiPlace.Forecast = f;

            if (url.Contains("pohorje"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/2_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_g.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_f.jpg", 
                "http://www.snezni-telefon.si/Images/Kamere/2_j.jpg"
            };
            }

            else if (url.Contains("kranjska"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.kr-gora.si/imagelib/source/webcams/cam02_krgora-01.jpg",
                "http://www.kr-gora.si/imagelib/source/webcams/cam01_krgora-01.jpg",
            };
            }

            else if (url.Contains("cerkno"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/images/kamere/5_b.jpg",
                "http://www.snezni-telefon.si/images/kamere/5.jpg",
                "http://www.snezni-telefon.si/images/kamere/5_d.jpg",
                "http://www.snezni-telefon.si/images/kamere/5_c.jpg"
            };
            }

            else if (url.Contains("vogel"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/6_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_d.jpg",
            };
            }

            else if (url.Contains("soriska"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/8_0.jpg"
            };
            }

            else if (url.Contains("vogel"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/6_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_d.jpg",
                "http://www.snezni-telefon.si/images/kamere/5_c.jpg"
            };
            }

            else if (url.Contains("kope"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/9_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/9_0_b.jpg",
            };
            }

            else if (url.Contains("kope"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/9_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/9_0_b.jpg",
            };
            }

            else if (url.Contains("golte"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/10_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_d.jpg"
            };
            }

            else if (url.Contains("stari-vrh"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/12_e.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/12_f.jpg",
            };
            }

            else if (url.Contains("golte"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/10_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_d.jpg"
            };
            }

            else if (url.Contains("javornik"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/17_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/17_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/17_d.jpg"   
            };
            }

            else if (url.Contains("crna"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/20.jpg",
            };
            }

            else if (url.Contains("poseka"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/29.jpg",
            };
            }

            else if (url.Contains("poseka"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/40_0.jpg",
            };
            }

            try
            {
                //potegnemo trenutno zadnjo vrstico da iz nje potegnemo snowlevel, če ga nimamo

                comm.Parameters.Clear();
                comm.CommandText = string.Format("select snowLevel from SkiSlope where zap_st = @zapSt and name like '{0}%'", skiPlace.Name);
                comm.Parameters.Add(new SqlParameter("@zapSt", zapSt-1));
                comm.Parameters.Add(new SqlParameter("@name", skiPlace.Name));
                int snowLevel_last = Convert.ToInt32(comm.ExecuteScalar());

                comm.Parameters.Clear();
                comm.CommandText = @"insert into SkiSlope 
                (name, height, updatedAt, updatedAtSystem, cam1, cam2, cam3, cam4, cam5, cam6, snowlevel, temperature_1, windspeed_1, description_1,
                descriptionstring_1, time_1, cloudiness_1, temperature_2, description_2, time_2, windspeed_2, temperature_3, description_3, 
                time_3, windspeed_3, temperature_4, description_4, 
                time_4, windspeed_4, temperature_5, description_5, 
                time_5, windspeed_5, zap_st) values (@name, @height, @updatedAt, @updatedAtSystem, @cam1, @cam2, @cam3, @cam4, @cam5, @cam6, @snowlevel, 
                @temperature_1, @windspeed_1, @description_1,
                @descriptionstring_1, @time_1, @cloudiness_1, @temperature_2, @description_2, @time_2, @windspeed_2, @temperature_3, @description_3, 
                @time_3, @windspeed_3, @temperature_4, @description_4, 
                @time_4, @windspeed_4, @temperature_5, @description_5, 
                @time_5, @windspeed_5, @zap_st)";

                comm.Parameters.Add(new SqlParameter("@name", skiPlace.Name));
                comm.Parameters.Add(new SqlParameter("@height", skiPlace.Height));
                comm.Parameters.Add(new SqlParameter("@updatedAt", skiPlace.UpdatedAt));
                comm.Parameters.Add(new SqlParameter("@updatedAtSystem", DateTime.UtcNow));

                #region cams
                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 0 && skiPlace.CamsUrlList[0] != null)
                    comm.Parameters.Add(new SqlParameter("@cam1", skiPlace.CamsUrlList[0]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam1", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 1 && skiPlace.CamsUrlList[1] != null)
                    comm.Parameters.Add(new SqlParameter("@cam2", skiPlace.CamsUrlList[1]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam2", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 2 && skiPlace.CamsUrlList[2] != null)
                    comm.Parameters.Add(new SqlParameter("@cam3", skiPlace.CamsUrlList[2]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam3", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 3 && skiPlace.CamsUrlList[3] != null)
                    comm.Parameters.Add(new SqlParameter("@cam4", skiPlace.CamsUrlList[3]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam4", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 4 && skiPlace.CamsUrlList[4] != null)
                    comm.Parameters.Add(new SqlParameter("@cam5", skiPlace.CamsUrlList[4]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam5", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 5 && skiPlace.CamsUrlList[5] != null)
                    comm.Parameters.Add(new SqlParameter("@cam6", skiPlace.CamsUrlList[5]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam6", ""));
                #endregion

                if (skiPlace.CurrentSnowLevel == 0)
                    skiPlace.CurrentSnowLevel = snowLevel_last;

                comm.Parameters.Add(new SqlParameter("@snowlevel", skiPlace.CurrentSnowLevel));
                comm.Parameters.Add(new SqlParameter("@temperature_1", skiPlace.CurrentWeather.Temperature));
                comm.Parameters.Add(new SqlParameter("@windspeed_1", skiPlace.CurrentWeather.WindSpeed));
                comm.Parameters.Add(new SqlParameter("@description_1", skiPlace.CurrentWeather.Description));
                comm.Parameters.Add(new SqlParameter("@descriptionstring_1", skiPlace.CurrentWeather.DescriptionString));
                comm.Parameters.Add(new SqlParameter("@time_1", ""));
                comm.Parameters.Add(new SqlParameter("@cloudiness_1", skiPlace.CurrentWeather.Cloudiness));

                comm.Parameters.Add(new SqlParameter("@temperature_2", skiPlace.Forecast.TodayMorning.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_2", skiPlace.Forecast.TodayMorning.Description));
                comm.Parameters.Add(new SqlParameter("@time_2", skiPlace.Forecast.TodayMorning.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_2", skiPlace.Forecast.TodayMorning.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@temperature_3", skiPlace.Forecast.TodayAfternoon.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_3", skiPlace.Forecast.TomorrowMorning.Description));
                comm.Parameters.Add(new SqlParameter("@time_3", skiPlace.Forecast.TodayAfternoon.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_3", skiPlace.Forecast.TodayAfternoon.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@temperature_4", skiPlace.Forecast.TomorrowMorning.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_4", skiPlace.Forecast.TomorrowMorning.Description));
                comm.Parameters.Add(new SqlParameter("@time_4", skiPlace.Forecast.TomorrowMorning.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_4", skiPlace.Forecast.TomorrowMorning.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@temperature_5", skiPlace.Forecast.TomorrowAfternoon.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_5", skiPlace.Forecast.TomorrowAfternoon.Description));
                comm.Parameters.Add(new SqlParameter("@time_5", skiPlace.Forecast.TomorrowAfternoon.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_5", skiPlace.Forecast.TomorrowAfternoon.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@zap_st", zapSt));
                comm.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                //send mail 
            }

            return skiPlace;
        }

        public Dictionary<string, Tuple<int, string, int>> ParseSkiTab(string htmlString, Dictionary<string, Tuple<int, string, int>> dict)
        {
            try
            {
                var document = new HtmlDocument();
                document.Load(new StringReader(htmlString));

                var name = document.DocumentNode.Descendants("span").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("data_name")).SingleOrDefault();
                var weather = document.DocumentNode.Descendants("img").Where(d => d.Attributes.Contains("alt")).First();

                var snow = document.DocumentNode.Descendants("span").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("data_snow") && !d.Attributes["class"].Value.Contains("data_snow_down")).SingleOrDefault();
                var snowDown = document.DocumentNode.Descendants("span").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("data_snow_down")).SingleOrDefault();

                var sth = document.DocumentNode.Descendants("span").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("data_temp"));
                var temperature = document.DocumentNode.Descendants("span").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("data_temp")).First();
                
                if (name == null)
                    return dict;

                string skiSlopeName = name.InnerText;
                int temperatureValue = Convert.ToInt32(temperature.InnerText);

                int s1 = 0;
                int s2 = 0;

                if (snow != null && snow.InnerText.Length > 1)
                    s1 = Convert.ToInt32(snow.InnerText.Trim());
                if (snowDown != null && snowDown.InnerText.Length > 1)
                    s2 = Convert.ToInt32(snowDown.InnerText.Trim());

                int snowLevel = s1 + s2;

                string weatherDesc = weather.Attributes["alt"].Value.ToString();

                dict.Add(skiSlopeName, Tuple.Create(snowLevel, weatherDesc, temperatureValue));

                return dict;
            }
            catch
            {
                return dict;
            }
        }

        public DataTable GetLastEntry()
        {
            DataTable dt = new DataTable();

            string query = @"select * from SkiSlope
                            where zap_st = (
                            select max(zap_st)
                            from SkiSlope)";
            comm.CommandText = query;

            SqlDataAdapter da = new SqlDataAdapter(comm);
            da.Fill(dt);
            da.Dispose();

            return dt;
        }

        public List<SkiPlace> CreateObjects(DataTable dt)
        {
            List<SkiPlace> skiPlaces = new List<SkiPlace>();

            //if (((DateTime)dt.Rows[0].Field<DateTime>("UpdatedAtSystem")) < DateTime.UtcNow.AddMinutes(-15))
            //    return null;

            try
            {
                foreach (DataRow row in dt.Rows)
                {
                    string name = row.Field<string>("Name");
                    int height = row.Field<int>("Height");
                    string updatedAt = row.Field<string>("UpdatedAt");
                    DateTime updatedAtSystem = (DateTime)row.Field<DateTime>("UpdatedAtSystem");

                    string cam1 = row.Field<string>("cam1").Trim();
                    string cam2 = row.Field<string>("cam2").Trim();
                    string cam3 = row.Field<string>("cam3").Trim();
                    string cam4 = row.Field<string>("cam4").Trim();
                    string cam5 = row.Field<string>("cam5").Trim();
                    string cam6 = row.Field<string>("cam6").Trim();

                    string[] cams = new string[] { cam1, cam2, cam3, cam4, cam5, cam6 };

                    int snowLevel = row.Field<int>("SnowLevel");
                    int temperature_1 = row.Field<int>("temperature_1");
                    string windspeed_1 = row.Field<string>("windspeed_1");
                    int description_1 = row.Field<int>("description_1");
                    string descriptionString_1 = row.Field<string>("descriptionString_1");
                    string time_1 = row.Field<string>("time_1");
                    string cloudiness_1 = row.Field<string>("cloudiness_1");

                    int temperature_2 = row.Field<int>("temperature_2");
                    int description_2 = row.Field<int>("description_2");
                    string time_2 = row.Field<string>("time_2");
                    string windspeed_2 = row.Field<string>("windspeed_2");

                    int temperature_3 = row.Field<int>("temperature_3");
                    int description_3 = row.Field<int>("description_3");
                    string time_3 = row.Field<string>("time_3");
                    string windspeed_3 = row.Field<string>("windspeed_3");

                    int temperature_4 = row.Field<int>("temperature_4");
                    int description_4 = row.Field<int>("description_4");
                    string time_4 = row.Field<string>("time_4");
                    string windspeed_4 = row.Field<string>("windspeed_4");

                    int temperature_5 = row.Field<int>("temperature_5");
                    int description_5 = row.Field<int>("description_5");
                    string time_5 = row.Field<string>("time_5");
                    string windspeed_5 = row.Field<string>("windspeed_5");

                    SkiPlace skiPlace = new SkiPlace();

                    Weather w1 = new Weather();
                    Weather w2 = new Weather();
                    Weather w3 = new Weather();
                    Weather w4 = new Weather();
                    Weather w5 = new Weather();

                    skiPlace.Name = name.Trim();
                    skiPlace.CurrentSnowLevel = snowLevel;
                    skiPlace.Height = height;
                    skiPlace.UpdatedAt = updatedAt.Trim();
                    skiPlace.CamsUrlList = cams.Where(c => c != null && c != string.Empty && c != "''").ToList<string>();

                    w1.Cloudiness = cloudiness_1.Trim();
                    w1.Description = (Weather.WeatherState)description_1;
                    w1.DescriptionString = descriptionString_1.Trim();
                    w1.Temperature = temperature_1;
                    w1.WindSpeed = windspeed_1.Trim();
                    w1.Time = time_1.Trim();

                    skiPlace.CurrentWeather = w1;

                    Forecast forecast = new Forecast();

                    w2.Time = time_2.Trim();
                    w2.Description = (Weather.WeatherState)description_2;
                    w2.WindSpeed = windspeed_2.Trim();
                    w2.Temperature = temperature_2;

                    w3.Time = time_3.Trim();
                    w3.Description = (Weather.WeatherState)description_3;
                    w3.WindSpeed = windspeed_3.Trim();
                    w3.Temperature = temperature_3;

                    w4.Time = time_4.Trim();
                    w4.Description = (Weather.WeatherState)description_4;
                    w4.WindSpeed = windspeed_4.Trim();
                    w4.Temperature = temperature_4;

                    w5.Time = time_5.Trim();
                    w5.Description = (Weather.WeatherState)description_5;
                    w5.WindSpeed = windspeed_5.Trim();
                    w5.Temperature = temperature_5;

                    forecast.TodayMorning = w2;
                    forecast.TodayAfternoon = w3;
                    forecast.TomorrowMorning = w4;
                    forecast.TomorrowAfternoon = w5;

                    skiPlace.Forecast = forecast;

                    skiPlaces.Add(skiPlace);

                }
            }
            catch(Exception ex)
            {
                //send mail
                return null;
            }

            return skiPlaces;
        }

        public String DownloadString(WebClient webClient, String address, Encoding encoding)
        {
            // this makes sure we download stuff in the right encoding.

            byte[] buffer = new byte[0];
            try
            {
                buffer = webClient.DownloadData(address);
            }
            catch (Exception)
            {

                Thread.Sleep(1000);
                buffer = webClient.DownloadData(address);
            }

            byte[] bom = encoding.GetPreamble();

            if ((0 == bom.Length) || (buffer.Length < bom.Length))
            {
                return encoding.GetString(buffer);
            }

            for (int i = 0; i < bom.Length; i++)
            {
                if (buffer[i] != bom[i])
                {
                    return encoding.GetString(buffer);
                }
            }

            return encoding.GetString(buffer, bom.Length, buffer.Length - bom.Length);
        }





        public List<SkiPlace> DataFromHribi(Dictionary<string, Tuple<int, string, int>> snezniTelefonDict)
        {
            List<SkiPlace> skiPlaces = new List<SkiPlace>();
            Dictionary<string, string> urls = new Dictionary<String, string> 
            {
                {"Cerkno", "http://www.hribi.net/vreme_smucisce/cerkno/5919"},
                {"M. Pohorje", "http://www.hribi.net/vreme_smucisce/mariborsko_pohorje/5916"},
                {"Rogla", "http://www.hribi.net/vreme_smucisce/rogla/5917"},
                {"Kranjska Gora", "http://www.hribi.net/vreme_smucisce/kranjska_gora/5918"},
                {"Vogel", "http://www.hribi.net/vreme_smucisce/vogel/5920"},
                {"Kanin", "http://www.hribi.net/vreme_smucisce/kanin_sella_nevea/15"},
                {"Soriška planina", "http://www.hribi.net/vreme_smucisce/soriska_planina/5921"},
                {"Kope", "http://www.hribi.net/vreme_smucisce/kope/5922"},
                {"Golte", "http://www.hribi.net/vreme_smucisce/golte/5923"},
                {"Stari vrh", "http://www.hribi.net/vreme_smucisce/stari_vrh/5925"},
                {"Bela (Gače)", "http://www.hribi.net/vreme_smucisce/sc_bela/5926"},
                {"Javornik", "http://www.hribi.net/vreme_smucisce/javornik/5930"},
                {"Črna na Koroškem", "http://www.hribi.net/vreme_smucisce/crna_na_koroskem/5933"},
                {"Poseka", "http://www.hribi.net/vreme_smucisce/poseka/5940"},
                {"SKI Bor", "http://www.hribi.net/vreme_smucisce/ski_bor_-_crni_vrh/5945"},
                {"Sviščaki", "http://www.hribi.net/vreme_smucisce/sviscaki/5946"},
                {"Bukovnik", "http://www.hribi.net/vreme_smucisce/bukovnik/5948"},
            };


            comm.CommandText = "select max(zap_st) from SkiSlope";
            int zapSt = Convert.ToInt32(comm.ExecuteScalar());
            zapSt++;


            foreach (var pair in urls)
            {
                skiPlaces.Add(ParseHribi(pair.Key, pair.Value, snezniTelefonDict, zapSt));
            }
            return skiPlaces;
        }

        public SkiPlace ParseHribi(string skiName, string url, Dictionary<string, Tuple<int, string, int>> snezniTelefonDict, int zapSt)
        {

            WebClient client = new WebClient();
            string htmlCode = DownloadString(client, url, Encoding.UTF8);

            HtmlDocument document = new HtmlDocument();
            string htmlString = htmlCode;
            htmlString = htmlString.Substring(htmlString.Substring(htmlString.Substring(htmlString.IndexOf("Vremenska napoved") + 1).IndexOf("Vremenska napoved")).IndexOf("Vremenska napoved"));
            document.LoadHtml(htmlString);

            string heightS = "";

            if (skiName.ToLower().Contains("cerkno"))
                heightS = "1300";
            else if (skiName.ToLower().Contains("pohorje"))
                heightS = "1327";
            else if (skiName.ToLower().Contains("bukovnik"))
                heightS = "625";
            else if (skiName.ToLower().Contains("kanin"))
                heightS = "2300";
            else if (skiName.ToLower().Contains("kope"))
                heightS = "1524";
            else if (skiName.ToLower().Contains("kranjska"))
                heightS = "1570";
            else if (skiName.ToLower().Contains("krvav"))
                heightS = "1971";
            else if (skiName.ToLower().Contains("rogla"))
                heightS = "1517";
            else if (skiName.ToLower().Contains("vogel"))
                heightS = "1800";
            else if (skiName.ToLower().Contains("črna"))
                heightS = "789";
            else if (skiName.ToLower().Contains("bela"))
                heightS = "965";
            else if (skiName.ToLower().Contains("golte"))
                heightS = "1600";
            else if (skiName.ToLower().Contains("javornik"))
                heightS = "1220";
            else if (skiName.ToLower().Contains("bor"))
                heightS = "780";
            else if (skiName.ToLower().Contains("poseka"))
                heightS = "550";
            else if (skiName.ToLower().Contains("soriška"))
                heightS = "1550";
            else if (skiName.ToLower().Contains("stari"))
                heightS = "1210";
            else if (skiName.ToLower().Contains("sviščaki"))
                heightS = "1330";

            string tempS = "0";
            string weatherDescS = "8";

            string weatherDescIndexS = "";

            System.Random RandNum = new System.Random();
            int myRandomNumber = RandNum.Next(70, 100);

            string oblacnostS = myRandomNumber.ToString();


            string veterS = myRandomNumber > 88 ? "15-20" : "10-20";
            DateTime date = DateTime.UtcNow.AddHours(1);


            string refreshedS = string.Format("Osveženo danes, {0} ob {1}", date.ToString("dd.MM.yyy"), date.ToString("HH:mm:ss"));

            //se iz sneznega telefona vzamemo ce je kej pametnega

            int snowLevel = 0;

            foreach (KeyValuePair<String, Tuple<int, string, int>> entry in snezniTelefonDict)
            {
                if ((skiName.Split(' ').ToList()).Intersect(entry.Key.Split(' ').ToList()).Count() > 0)
                {
                    snowLevel = entry.Value.Item1;
                    if (entry.Value.Item2.Length > 2)
                    {
                        weatherDescS = entry.Value.Item2.Replace("&nbsp;", "").Trim();
                        var a = weatherDescS.ToCharArray();
                        a[0] = Char.ToUpper(a[0]);
                        weatherDescS = new string(a);
                        tempS = entry.Value.Item3.ToString();
                    }

                }
            }

            //danes napoved

            string dan = TranslateDay(DateTime.UtcNow.ToString("dddd"));
            string danesDatumS = dan + ", " + DateTime.UtcNow.ToString("dd.MMMM");


            string danesDopoldneTemps = tempS.ToString();
            string danesDopoldneVeterS = veterS;

            string danesDopoldneweatherDescIndexS = ""; 
            string index="3";

            string temp_d = weatherDescS;

            temp_d = temp_d.ToLower();
                
            if (temp_d.Contains("sne"))
                index = "9";
            else if (temp_d.Contains("megl"))
                index = "2";
            else if (temp_d.Contains("pret") && temp_d.Contains("obl"))
                index = "4";
            else if (temp_d.Contains("delno") && temp_d.Contains("obl"))
                index = "3";
            else if ((temp_d.Contains("pret") || temp_d.Contains("deln")) && temp_d.Contains("jasn"))
                index = "3";
            else if (temp_d.Contains("obl"))
                index = "8";
            else if (temp_d == "jasno")
                index = "0";

            danesDopoldneweatherDescIndexS = index;

            string danesPopoldneTemps = tempS.ToString();

            string danesPopoldneVeterS = veterS;
            string danesPopoldneweatherDescIndexS = danesDopoldneweatherDescIndexS;

            //jutri napoved


            dan = TranslateDay(DateTime.UtcNow.AddDays(1).ToString("dddd"));
            string jutriDatumS = dan + ", " + DateTime.UtcNow.AddDays(1).ToString("dd.MMMM");

            HtmlNode jutriDopoldneTemp = document.DocumentNode.SelectSingleNode(@"//table/tr[8]/td[3]/table/tr[3]/td");
            string jutriDopoldneTemps;
            if (jutriDopoldneTemp != null)
                jutriDopoldneTemps = jutriDopoldneTemp.InnerHtml.Replace("&deg;C", "").Replace("°C", "").Trim();
            else
                jutriDopoldneTemps = "N/A";

            string jutriDopoldneVeterS = "10-15 km/h";

            HtmlNode jutriDopoldneweatherDescIndex = document.DocumentNode.SelectSingleNode(@"//table/tr[8]/td[3]/table/tr[2]/td/img");
            string jutriDopoldneweatherDescIndexS = "4";
            string temp_2 = "4";
            if (jutriDopoldneweatherDescIndex != null)
                temp_2 = Path.GetFileNameWithoutExtension(jutriDopoldneweatherDescIndex.Attributes["src"].Value);
            
            if (temp_2.StartsWith("1_1"))
                temp_2 = "0";
            else if (temp_2.StartsWith("1_2"))
                temp_2 = "3";
            else if (temp_2.StartsWith("1_3"))
                temp_2 = "4";
            else if (temp_2.StartsWith("2_1"))
                temp_2 = "10";
            else if (temp_2.StartsWith("2_2"))
                temp_2 = "10";
            else if (temp_2.StartsWith("2_3"))
                temp_2 = "9";
            else if (temp_2.StartsWith("3_1"))
                temp_2 = "10";
            else if (temp_2.StartsWith("3_2"))
                temp_2 = "10";
            else if (temp_2.StartsWith("3_3"))
                temp_2 = "9";
            else if (temp_2.StartsWith("4_1"))
                temp_2 = "10";
            else if (temp_2.StartsWith("4_2"))
                temp_2 = "10";
            else if (temp_2.StartsWith("4_3"))
                temp_2 = "9";

            jutriDopoldneweatherDescIndexS = temp_2;

            HtmlNode jutriPopoldneTemp = document.DocumentNode.SelectSingleNode(@"//table/tr[8]/td[6]/table/tr[3]/td");
            string jutriPopoldneTemps;

            if (jutriPopoldneTemp != null)
                jutriPopoldneTemps = jutriPopoldneTemp.InnerHtml.Replace("&deg;C", "").Replace("°C", "").Trim();
            else
                jutriPopoldneTemps = "N/A";

            string jutriPopoldneVeterS = "10-15 km/h";

            HtmlNode jutriPopoldneweatherDescIndex = document.DocumentNode.SelectSingleNode(@"//table/tr[8]/td[6]/table/tr[2]/td/img");
            string jutriPopoldneweatherDescIndexS;
            temp_2 = "4";
            if (jutriPopoldneweatherDescIndex != null)
                temp_2 = Path.GetFileNameWithoutExtension(jutriPopoldneweatherDescIndex.Attributes["src"].Value);

            if (temp_2.StartsWith("1_1"))
                temp_2 = "0";
            else if (temp_2.StartsWith("1_2"))
                temp_2 = "3";
            else if (temp_2.StartsWith("1_3"))
                temp_2 = "4";
            else if (temp_2.StartsWith("2_1"))
                temp_2 = "10";
            else if (temp_2.StartsWith("2_2"))
                temp_2 = "10";
            else if (temp_2.StartsWith("2_3"))
                temp_2 = "9";
            else if (temp_2.StartsWith("3_1"))
                temp_2 = "10";
            else if (temp_2.StartsWith("3_2"))
                temp_2 = "10";
            else if (temp_2.StartsWith("3_3"))
                temp_2 = "9";
            else if (temp_2.StartsWith("4_1"))
                temp_2 = "10";
            else if (temp_2.StartsWith("4_2"))
                temp_2 = "10";
            else if (temp_2.StartsWith("4_3"))
                temp_2 = "9";

            jutriPopoldneweatherDescIndexS = temp_2;


            Debug.WriteLine(
                url + ": " + Environment.NewLine
                + "Ime: " + skiName + Environment.NewLine
                + "Višina: " + heightS + Environment.NewLine
                + "Temperatura: " + tempS + Environment.NewLine
                + "Sneg: " + snowLevel + Environment.NewLine
                + "Vreme: " + weatherDescS + Environment.NewLine
                + "Vreme index: " + weatherDescIndexS + Environment.NewLine
                + "Veter: " + veterS + Environment.NewLine
                + "Oblacnost: " + oblacnostS + Environment.NewLine
                + "Osvezeno: " + refreshedS + Environment.NewLine
                + "Napoved " + Environment.NewLine
                + "\t Danes (" + danesDatumS + ")" + Environment.NewLine
                + "\t\t Dopoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + danesDopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + danesDopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + danesDopoldneVeterS + Environment.NewLine
                + "\t\t Popoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + danesPopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + danesPopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + danesPopoldneVeterS + Environment.NewLine
                + "\t Jutri (" + jutriDatumS + ")" + Environment.NewLine
                + "\t\t Dopoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + jutriDopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + jutriDopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + jutriDopoldneVeterS + Environment.NewLine
                + "\t\t Popoldne " + Environment.NewLine
                + "\t\t\t Vreme index: " + jutriPopoldneweatherDescIndexS + Environment.NewLine
                + "\t\t\t Temperatura: " + jutriPopoldneTemps + Environment.NewLine
                + "\t\t\t Veter: " + jutriPopoldneVeterS
                );

            Debug.WriteLine(Environment.NewLine);


            SkiPlace skiPlace = new SkiPlace();

            skiPlace.Name = skiName;
            skiPlace.CurrentSnowLevel = snowLevel;
            skiPlace.Height = Convert.ToInt32(heightS.Replace("m", "").Replace(":", "").Trim());
            skiPlace.UpdatedAt = refreshedS;

            Weather weather = new Weather();
            weather.Temperature = Convert.ToInt32(tempS);
            //weather.Description = (Weather.WeatherState)Convert.ToInt32(weatherDescIndexS);
            weather.DescriptionString = weatherDescS;
            weather.WindSpeed = veterS;
            weather.Cloudiness = oblacnostS.Replace("%", "").Trim();

            //še mal extra potweakamo ikonco iz opisa

            if (weatherDescS != null && weatherDescS.Length > 2)
            {
                weatherDescS = weatherDescS.Trim().ToLower();

                if (weatherDescS == "jasno" || weatherDescS.Contains("sončn"))
                    weather.Description = (Weather.WeatherState)0;
                else if (weatherDescS.Contains("megl"))
                    weather.Description = (Weather.WeatherState)2;
                else if (weatherDescS.Contains("sneg") || weatherDescS.Contains("snež"))
                    weather.Description = (Weather.WeatherState)10;
                else if (weatherDescS.Contains("pretežno") && weatherDescS.Contains("oblačno"))
                    weather.Description = (Weather.WeatherState)13;
                else if (weatherDescS.Contains("pretežno") && weatherDescS.Contains("jasno"))
                    weather.Description = (Weather.WeatherState)4;
                else if (weatherDescS.Contains("megl"))
                    weather.Description = (Weather.WeatherState)2;
                else if (weatherDescS == "obla")
                    weather.Description = (Weather.WeatherState)8;
                else
                    weather.Description = (Weather.WeatherState)8;
            }

            Weather weatherTodayMorning = new Weather();
            weatherTodayMorning.Temperature = Convert.ToInt32(danesDopoldneTemps);
            weatherTodayMorning.Description = (Weather.WeatherState)Convert.ToInt32(danesDopoldneweatherDescIndexS);
            weatherTodayMorning.Time = danesDatumS;
            weatherTodayMorning.WindSpeed = danesDopoldneVeterS;

            Weather weatherTodayAfternoon = new Weather();
            weatherTodayAfternoon.Temperature = Convert.ToInt32(danesPopoldneTemps);
            weatherTodayAfternoon.Description = (Weather.WeatherState)Convert.ToInt32(danesPopoldneweatherDescIndexS);
            weatherTodayAfternoon.Time = danesDatumS;
            weatherTodayAfternoon.WindSpeed = danesPopoldneVeterS;

            Weather weatherTomorrowMorning = new Weather();
            weatherTomorrowMorning.Temperature = Convert.ToInt32(jutriDopoldneTemps);
            weatherTomorrowMorning.Description = (Weather.WeatherState)Convert.ToInt32(jutriDopoldneweatherDescIndexS);
            weatherTomorrowMorning.Time = jutriDatumS;
            weatherTomorrowMorning.WindSpeed = jutriDopoldneVeterS;

            Weather weatherTomorrowAfternoon = new Weather();
            weatherTomorrowAfternoon.Temperature = Convert.ToInt32(jutriPopoldneTemps);
            weatherTomorrowAfternoon.Description = (Weather.WeatherState)Convert.ToInt32(jutriPopoldneweatherDescIndexS);
            weatherTomorrowAfternoon.Time = jutriDatumS;
            weatherTomorrowAfternoon.WindSpeed = jutriPopoldneVeterS;


            Forecast f = new Forecast();

            f.TodayMorning = weatherTodayMorning;
            f.TodayAfternoon = weatherTodayAfternoon;
            f.TomorrowMorning = weatherTomorrowMorning;
            f.TomorrowAfternoon = weatherTomorrowAfternoon;

            skiPlace.CurrentWeather = weather;
            skiPlace.Forecast = f;

            string tempforname = skiName;
            skiName = skiName.ToLower();

            # region cams
            if (skiName.Contains("pohorje"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/2_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_g.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/2_f.jpg", 
                "http://www.snezni-telefon.si/Images/Kamere/2_j.jpg"
            };
            }

            else if (skiName.Contains("kranjska"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.kr-gora.si/imagelib/source/webcams/cam02_krgora-01.jpg",
                "http://www.kr-gora.si/imagelib/source/webcams/cam01_krgora-01.jpg",
            };
            }

            else if (skiName.Contains("cerkno"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/images/kamere/5_b.jpg",
                "http://www.snezni-telefon.si/images/kamere/5.jpg",
                "http://www.snezni-telefon.si/images/kamere/5_d.jpg",
                "http://www.snezni-telefon.si/images/kamere/5_c.jpg"
            };
            }

            else if (skiName.Contains("vogel"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/6_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_d.jpg",
            };
            }

            else if (skiName.Contains("soriška"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/8_0.jpg"
            };
            }

            else if (skiName.Contains("vogel"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/6_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/6_d.jpg",
                "http://www.snezni-telefon.si/images/kamere/5_c.jpg"
            };
            }

            else if (skiName.Contains("kope"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/9_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/9_0_b.jpg",
            };
            }

            else if (skiName.Contains("kope"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/9_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/9_0_b.jpg",
            };
            }

            else if (skiName.Contains("golte"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/10_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_d.jpg"
            };
            }

            else if (skiName.Contains("stari") && url.Contains("vrh"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/12_e.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/12_f.jpg",
            };
            }

            else if (skiName.Contains("golte"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/10_a.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_0.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/10_d.jpg"
            };
            }

            else if (skiName.Contains("javornik"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/17_b.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/17_c.jpg",
                "http://www.snezni-telefon.si/Images/Kamere/17_d.jpg"   
            };
            }

            else if (skiName.Contains("crna"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/20.jpg",
            };
            }

            else if (skiName.Contains("poseka"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/29.jpg",
            };
            }

            else if (skiName.Contains("poseka"))
            {
                skiPlace.CamsUrlList = new List<string> 
            {
                "http://www.snezni-telefon.si/Images/Kamere/40_0.jpg",
            };
            }
            skiName = tempforname;
            #endregion 

            try
            {
                comm.Parameters.Clear();
                comm.CommandText = @"insert into SkiSlope 
                (name, height, updatedAt, updatedAtSystem, cam1, cam2, cam3, cam4, cam5, cam6, snowlevel, temperature_1, windspeed_1, description_1,
                descriptionstring_1, time_1, cloudiness_1, temperature_2, description_2, time_2, windspeed_2, temperature_3, description_3, 
                time_3, windspeed_3, temperature_4, description_4, 
                time_4, windspeed_4, temperature_5, description_5, 
                time_5, windspeed_5, zap_st) values (@name, @height, @updatedAt, @updatedAtSystem, @cam1, @cam2, @cam3, @cam4, @cam5, @cam6, @snowlevel, 
                @temperature_1, @windspeed_1, @description_1,
                @descriptionstring_1, @time_1, @cloudiness_1, @temperature_2, @description_2, @time_2, @windspeed_2, @temperature_3, @description_3, 
                @time_3, @windspeed_3, @temperature_4, @description_4, 
                @time_4, @windspeed_4, @temperature_5, @description_5, 
                @time_5, @windspeed_5, @zap_st)";

                comm.Parameters.Add(new SqlParameter("@name", skiPlace.Name));
                comm.Parameters.Add(new SqlParameter("@height", skiPlace.Height));
                comm.Parameters.Add(new SqlParameter("@updatedAt", skiPlace.UpdatedAt));
                comm.Parameters.Add(new SqlParameter("@updatedAtSystem", DateTime.UtcNow));

                #region cams
                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 0 && skiPlace.CamsUrlList[0] != null)
                    comm.Parameters.Add(new SqlParameter("@cam1", skiPlace.CamsUrlList[0]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam1", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 1 && skiPlace.CamsUrlList[1] != null)
                    comm.Parameters.Add(new SqlParameter("@cam2", skiPlace.CamsUrlList[1]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam2", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 2 && skiPlace.CamsUrlList[2] != null)
                    comm.Parameters.Add(new SqlParameter("@cam3", skiPlace.CamsUrlList[2]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam3", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 3 && skiPlace.CamsUrlList[3] != null)
                    comm.Parameters.Add(new SqlParameter("@cam4", skiPlace.CamsUrlList[3]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam4", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 4 && skiPlace.CamsUrlList[4] != null)
                    comm.Parameters.Add(new SqlParameter("@cam5", skiPlace.CamsUrlList[4]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam5", ""));

                if (skiPlace.CamsUrlList != null && skiPlace.CamsUrlList.Count > 5 && skiPlace.CamsUrlList[5] != null)
                    comm.Parameters.Add(new SqlParameter("@cam6", skiPlace.CamsUrlList[5]));
                else
                    comm.Parameters.Add(new SqlParameter("@cam6", ""));
                #endregion
                comm.Parameters.Add(new SqlParameter("@snowlevel", skiPlace.CurrentSnowLevel));
                comm.Parameters.Add(new SqlParameter("@temperature_1", skiPlace.CurrentWeather.Temperature));
                comm.Parameters.Add(new SqlParameter("@windspeed_1", skiPlace.CurrentWeather.WindSpeed));
                comm.Parameters.Add(new SqlParameter("@description_1", skiPlace.CurrentWeather.Description));
                comm.Parameters.Add(new SqlParameter("@descriptionstring_1", skiPlace.CurrentWeather.DescriptionString));
                comm.Parameters.Add(new SqlParameter("@time_1", ""));
                comm.Parameters.Add(new SqlParameter("@cloudiness_1", skiPlace.CurrentWeather.Cloudiness));

                comm.Parameters.Add(new SqlParameter("@temperature_2", skiPlace.Forecast.TodayMorning.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_2", skiPlace.Forecast.TodayMorning.Description));
                comm.Parameters.Add(new SqlParameter("@time_2", skiPlace.Forecast.TodayMorning.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_2", skiPlace.Forecast.TodayMorning.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@temperature_3", skiPlace.Forecast.TodayAfternoon.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_3", skiPlace.Forecast.TomorrowMorning.Description));
                comm.Parameters.Add(new SqlParameter("@time_3", skiPlace.Forecast.TodayAfternoon.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_3", skiPlace.Forecast.TodayAfternoon.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@temperature_4", skiPlace.Forecast.TomorrowMorning.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_4", skiPlace.Forecast.TomorrowMorning.Description));
                comm.Parameters.Add(new SqlParameter("@time_4", skiPlace.Forecast.TomorrowMorning.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_4", skiPlace.Forecast.TomorrowMorning.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@temperature_5", skiPlace.Forecast.TomorrowAfternoon.Temperature));
                comm.Parameters.Add(new SqlParameter("@description_5", skiPlace.Forecast.TomorrowAfternoon.Description));
                comm.Parameters.Add(new SqlParameter("@time_5", skiPlace.Forecast.TomorrowAfternoon.Time));
                comm.Parameters.Add(new SqlParameter("@windspeed_5", skiPlace.Forecast.TomorrowAfternoon.WindSpeed));

                comm.Parameters.Add(new SqlParameter("@zap_st", zapSt));
                comm.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                //send mail 
            }

            return skiPlace;
        }

        public String TranslateDay(string day)
        {
            day = day.ToLower().Trim();
            if (day == "monday")
                day = "Ponedeljek";
            else if (day == "tuesday")
                day = "Torek";
            else if (day == "wednesday")
                day = "Sreda";
            else if (day == "thursday")
                day = "Četrtek";
            else if (day == "friday")
                day = "Petek";
            else if (day == "saturday")
                day = "Sobota";
            else if (day == "sunday")
                day = "Nedelja";

            var a = day.ToCharArray();
            a[0] = Char.ToUpper(a[0]);
            day = new string(a);

            return day;
        }





    }



}