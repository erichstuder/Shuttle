using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.IO.Compression;
using static Shuttle.Timetable;
using System.Xml;

namespace Timetable_Manager
{
    class Program
    {
        private const int GoogleApisRequestDelay_ms = 3000;

        private class TripsPostProcessing
        {
            public readonly List<string> allowedVillageNames;
            public readonly List<DateTime> activeDays;
            public readonly List<string> allowedTrip_short_names;

            public TripsPostProcessing(List<string> allowedVillageNames, List<DateTime> activeDays, List<string> allowedTrip_short_names)
            {
                this.allowedVillageNames = allowedVillageNames;
                this.activeDays = activeDays;
                this.allowedTrip_short_names = allowedTrip_short_names;
            }
        }

        // Defines a timetable in a region e.g. Flims, Laax, Falera, Sagogn
        // separation into regions is necessary as the route_short_name is not unique within a timetable (e.g. switzerland)
        private class TimetableRegion
        {
            public readonly List<Village> villages;
            public readonly List<string> route_short_names; //according to gtfs definition these are the line number ids that are unique for a region (hopefully)
            public readonly TripsPostProcessing tripsPostProcessing;
            public readonly double minLat;
            public readonly double maxLat;
            public readonly double minLng;
            public readonly double maxLng;
            //public List<String> stop_ids;

            public TimetableRegion(List<Village> villages, List<string> route_short_names, TripsPostProcessing tripsPostProcessing)
            {
                this.villages = villages;
                this.route_short_names = route_short_names;
                this.tripsPostProcessing = tripsPostProcessing;
                
                double minLat = double.MaxValue;
                double maxLat = double.MinValue;
                double minLng = double.MaxValue;
                double maxLng = double.MinValue;

                foreach (Village village in villages)
                {
                    //foreach (string postcode in village.postcodes)
                    {
                        string address = village.postal_code + "+" + village.administrative_area_level_2 + "+" + village.administrative_area_level_1 + "+" + village.country;

                        XmlElement documentElement = googleApisAddressRequest(address);

                        //HttpWebRequest request = HttpWebRequest.CreateHttp("https://maps.googleapis.com/maps/api/geocode/xml?address="+ address +"&language=en");
                        
                        //System.Threading.Thread.Sleep(GoogleApisRequestDelay_ms); //don't do too many requests
                        //WebResponse response = request.GetResponse();

                        //// Get the stream containing content returned by the server.
                        //Stream dataStream = response.GetResponseStream();
                        //// Open the stream using a StreamReader for easy access.
                        //StreamReader reader = new StreamReader(dataStream);
                        //// Read the content.
                        //string responseFromServer = reader.ReadToEnd();

                        //XmlDocument xmlDoc = new XmlDocument();
                        //xmlDoc.LoadXml(responseFromServer);
                        //XmlElement documentElement = xmlDoc.DocumentElement;


                        //XmlNode child = documentElement.FirstChild;
                        XmlNode geometry = documentElement.GetElementsByTagName("geometry").Item(0);
                        XmlNode bounds = geometry.SelectSingleNode("bounds");

                        XmlNode southwest = bounds.SelectSingleNode("southwest");
                        double minLatNew = double.Parse(southwest.SelectSingleNode("lat").FirstChild.Value);
                        double minLngNew = double.Parse(southwest.SelectSingleNode("lng").FirstChild.Value);

                        XmlNode northeast = bounds.SelectSingleNode("northeast");
                        double maxLatNew = double.Parse(northeast.SelectSingleNode("lat").FirstChild.Value);
                        double maxLngNew = double.Parse(northeast.SelectSingleNode("lng").FirstChild.Value);

                        minLat = Math.Min(minLat, minLatNew);
                        maxLat = Math.Max(maxLat, maxLatNew);
                        minLng = Math.Min(minLng, minLngNew);
                        maxLng = Math.Max(maxLng, maxLngNew);
                    }
                }
                this.minLat = minLat;
                this.maxLat = maxLat;
                this.minLng = minLng;
                this.maxLng = maxLng;
            }
        }

        // fields are defined according to google maps.org e.g. https://maps.googleapis.com/maps/api/geocode/xml?address=7018+Imboden%20District+Grisons+Switzerland&language=en
        private class Village
        {
            public readonly string postal_code;                  //in switzerland: 4 digits
            //public readonly string locality;                   //name of the village (according to postal_code) => taucht bei google maps nicht immer auf
            public readonly string administrative_area_level_2;  //e.g. Imboden, Surselva, ...
            public readonly string administrative_area_level_1;  //in switzerland: name of the kanton in german (long_name)
            public readonly string country;                      //country name in german

            public Village(string postal_code, string administrative_area_level_2, string administrative_area_level_1, string country)
            {
                this.postal_code = postal_code;
                this.administrative_area_level_2 = administrative_area_level_2;
                this.administrative_area_level_1 = administrative_area_level_1;
                this.country = country;
            }
        };



        static void Main(string[] args)
        {            
            //Unstimmigkeiten
            //
            //"Fidaz, Dorf" und "Fidaz, Post"
            //GTFS und HRDF kennen nur "Fidaz, Post"
            //FLF pdf-Fahrplan Winter kennt nur "Fidaz, Dorf"
            //FLF pdf-Fahrplan Sommer kennt nur "Fidaz, Post"
            //sbb.ch kennt die Suche nach beiden. Das Resultat zeigt aber immer "Fidaz, Dorf" an. Klickt man auf die Haltestelle, so heisst sie plötzlich "Fidaz, Post"
            //evtl. mal abklären: Was steht effektiv bei der Haltestelle?
            //=> Mal so lassen, aber weiter beobachten. Evtl. "Fidaz, Post" ändern in "Fidaz, Dorf/Post"
            //
            //direction_id immer 1
            //mindestens für FLF ist die direciton_id immer 1, obwohl die route_id gleich ist.
            //es gibt andere Verbindungen für die sie scheinbar richtig verwendet wird (siehe trips.txt).

            //Dörfer: Falera, Laax GR, Flims Waldhaus, Flims Dorf, Fidaz
            //Linien: 01 12 13 14 15 16 17 21 22 24 31 18 //Sommer Linien fehlen noch
            //Zeitraum: 10.12.2016 bis 17.04.2017
            //Zusatz: Nightliner

            int origWidth = Console.WindowWidth;
            int origHeight = Console.WindowHeight;
            //Console.SetWindowSize(213, 50);
            Console.BufferHeight = Int16.MaxValue-1;

            //define the timetables seperated in regions
            //seperation into regions is necessary as the route_short_name is not globally unique
            List<TimetableRegion> timetableRegions = new List<TimetableRegion>(new[] {
                new TimetableRegion(
                    new List<Village>(new[] {
                        new Village("7017", "Imboden",  "Graubünden", "Switzerland"), // Flims Dorf
                        new Village("7018", "Imboden",  "Graubünden", "Switzerland"), // Flims Waldhaus
                        new Village("7019", "Imboden",  "Graubünden", "Switzerland"), // Fidaz
                        new Village("7031", "Surselva", "Graubünden", "Switzerland"), // Laax Dorf
                        new Village("7032", "Surselva", "Graubünden", "Switzerland"), // Laax Murschetg
                        new Village("7153", "Surselva", "Graubünden", "Switzerland"), // Falera
                    }),
                
                    new List<string>(new[] { "1", "12", "13", "14", "15", "16", "17", "21", "22", "23", "24", "31", "18" } ),

                    null
                ),

                //new TimetableRegion(
                //    new List<Village>(new[] {
                //        new Village("7152", "Surselva", "Graubünden", "Switzerland"), // Sagogn
                //        new Village("7151", "Surselva", "Graubünden", "Switzerland"), // Schluein
                //    }),
                    
                //    new List<string>(new[] { "411" } ),

                //    new TripsPostProcessing(
                //        new List<string>() {"Schluein", "Sagogn", "Laax", "Falera"},
                //        new List<DateTime>() {createDateTimeList(new DateTime(TBD), new DateTime(TBD)) },
                //        TBD
                //    )
                //),

                //Ruschein Ladir???

                //new TimetableRegion(new List<Village>(new[] {
                //    new Village("7050", "Plessur",  "Graubünden", "Switzerland"), // Arosa

                //}), new List<string>(new[] { "061", "062" } ))
            });

            string timetablePath = "";
            Console.Write("Would you like to use your own timetable files? [Y/N]: ");
            string ans = Console.ReadLine();
            if (ans.ToUpper().Equals("Y"))
            {
                Console.Write("Enter path to unzipped timetable files: ");
                timetablePath = Console.ReadLine();
            }
            else
            {
                Console.Write("downloading timetable data ... ");
                timetablePath = downloadAndUnzipTimetableFiles();
                Console.WriteLine("done");
            }

            List<Trip> trips = new List<Trip>();
            
            foreach (TimetableRegion timetableRegion in timetableRegions)
            {
                Console.WriteLine("***** computing region with postal code of first village: " + timetableRegion.villages[0].postal_code);

                Console.Write("searching for relevant stop ids ... ");
                List<string> stop_ids = getStop_ids(timetableRegion, timetablePath);
                Console.WriteLine("done");

                Console.Write("getting relevant trip ids ... ");
                List<String> trip_ids = getTrip_ids(stop_ids, timetablePath);
                Console.WriteLine("done");

                Console.Write("creating trips ... ");
                List<Trip> trips_regional = getTrips(trip_ids, timetablePath);
                Console.WriteLine("done");

                Console.Write("adding stop names to trips ... ");
                trips_regional = getStop_names(trips_regional, timetablePath);
                Console.WriteLine("done");

                Console.Write("adding route ids to trips ... ");
                trips_regional = getRoute_id(trips_regional, timetablePath);
                Console.WriteLine("done");

                Console.Write("adding route short names to trips ... ");
                trips_regional = getRoute_short_name(trips_regional, timetablePath);
                Console.WriteLine("done");

                Console.Write("remove irrelevant trips ... ");
                trips_regional = getRelevantTrips(trips_regional, timetableRegion.route_short_names);
                Console.WriteLine("done");

                Console.Write("adding information of active days ... ");
                trips_regional = getActiveDays(trips_regional, timetablePath);
                Console.WriteLine("done");

                trips.AddRange(trips_regional);
            }

            trips = createGraph(trips);
            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            string tripsSerializedJson = JsonConvert.SerializeObject(trips, jsonSerializerSettings);
            File.WriteAllText("..\\..\\trips.json", tripsSerializedJson, System.Text.Encoding.UTF8);

            File.WriteAllText("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle\\Resources\\trips.json", tripsSerializedJson, System.Text.Encoding.UTF8);

            List <string> stopNames = getAllStopNames(trips);
            stopNames.Sort();
            string stop_namesSerializedJson = JsonConvert.SerializeObject(stopNames);
            File.WriteAllText("..\\..\\stop_names.json", stop_namesSerializedJson, System.Text.Encoding.UTF8);

            File.WriteAllText("..\\..\\..\\Shuttle_Xamarin\\Shuttle\\Shuttle\\Resources\\stop_names.json", stop_namesSerializedJson, System.Text.Encoding.UTF8);

            Console.Write("Finished! Presse Enter to close.");
            Console.ReadLine();
        }

        private static List<DateTime> createDateTimeList(DateTime first, DateTime last)
        {
            List<DateTime> list = new List<DateTime>();
            for (DateTime i = first; i <= last; i=i.AddDays(1))
            {
                list.Add(i);
            }
            return list;
        }

        private static string downloadAndUnzipTimetableFiles()
        {
            //sobald mehr als ein gtfs file verwendet wird, müssen diese zu einem file gemerged werden, damit das handling der trips einfacher wird
            //es sollen alle fahrpläne in den gleichen ordner gespeichert werden, aber diese heissen alle gleich. hier muss eine lösung gefunden werden.
            //https://github.com/google/transitfeed/wiki/Merge

            //at the moment there are only the files for switzerland
            string webAddress = "https://transitfeeds.com/p/sbb-cff-ffs/793/latest/download"; //(switzerland)

            //get the file name
            HttpWebRequest request = HttpWebRequest.CreateHttp(webAddress);
            WebResponse response = request.GetResponse();
            string fileName = System.IO.Path.GetFileName(response.ResponseUri.AbsoluteUri);

            //download the recent timetable file
            // System.IO.Path.GetTempPath(); C:\Users\erich\AppData\Local\Temp\Shuttle\timetableData\ch
            string timetableFolder = System.IO.Path.GetTempPath() + "Shuttle_"+ DateTime.Now.ToString("yyyy_MM_dd__HH_mm") + "\\timetableData";
            string filePath = timetableFolder + "\\" + fileName;
            if (!Directory.Exists(timetableFolder))
            {
               Directory.CreateDirectory(timetableFolder);
            }
            WebClient webClient = new WebClient();
            webClient.DownloadFile(webAddress, @filePath);

            //unzip the file
            ZipFile.ExtractToDirectory(filePath, timetableFolder);

            //sometimes there is a folder in the zip-file
            string[] directories = Directory.GetDirectories(timetableFolder);
            if (directories.Count() > 0)
            {
                timetableFolder = directories[0];
            }

            return timetableFolder;
        }

        private static List<Trip> getRelevantTrips(List<Trip> trips, List<string> relevant_route_short_names)
        {
            for(int n = trips.Count-1; n>=0; n--)
            {
                Trip trip = trips[n];
                if (!relevant_route_short_names.Contains(trip.route_short_name))
                {
                    trips.Remove(trip);
                }
            }
            return trips;
        }

        private static List<string> getStop_ids(TimetableRegion timetableRegion, string timetablePath)
        {
            List<string> stop_ids = new List<string>();

            using (TextReader fileReader = File.OpenText(timetablePath + "\\stops.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int stop_id_idx = Array.IndexOf(fieldHeaders, "stop_id");
                int stop_lat_idx = Array.IndexOf(fieldHeaders, "stop_lat");
                int stop_lon_idx = Array.IndexOf(fieldHeaders, "stop_lon");

                //stop_id,stop_code,stop_name,stop_desc,stop_lat,stop_lon,stop_elevation,zone_id,stop_url,location_type,parent_station,platform_code,ch_station_long_name,ch_station_synonym1,ch_station_synonym2,ch_station_synonym3,ch_station_synonym4

                while (csv.Read())
                {
                    string stop_id = csv.GetField(stop_id_idx);
                    double stop_lat = double.Parse(csv.GetField(stop_lat_idx));
                    double stop_lon = double.Parse(csv.GetField(stop_lon_idx));

                    //check if the station is probable to be in the region
                    if (stop_lat >= timetableRegion.minLat && stop_lat <= timetableRegion.maxLat && stop_lon >= timetableRegion.minLng && stop_lon <= timetableRegion.maxLng)
                    {
                        XmlElement documentElement = googleApisLatLngRequest(stop_lat + ", " + stop_lon);

                        //HttpWebRequest request = HttpWebRequest.CreateHttp("https://maps.googleapis.com/maps/api/geocode/xml?latlng=" + stop_lat + "," + stop_lon + "&language=en");

                        //System.Threading.Thread.Sleep(GoogleApisRequestDelay_ms); //don't do too many requests
                        //WebResponse response = request.GetResponse();

                        //// Get the stream containing content returned by the server.
                        //Stream dataStream = response.GetResponseStream();
                        //// Open the stream using a StreamReader for easy access.
                        //StreamReader reader = new StreamReader(dataStream);
                        //// Read the content.
                        //string responseFromServer = reader.ReadToEnd();

                        //XmlDocument xmlDoc = new XmlDocument();
                        //xmlDoc.LoadXml(responseFromServer);
                        //XmlElement documentElement = xmlDoc.DocumentElement;

                        XmlNodeList results = documentElement.GetElementsByTagName("result");
                        XmlNode result = null;
                        foreach (XmlNode r in results)
                        {
                            XmlNodeList types = r.SelectNodes("type");

                            //XmlNode firstChild = r.FirstChild;

                            foreach(XmlNode type in types)
                            {
                                switch(type.FirstChild.Value)
                                {
                                    case "bus_station":
                                    case "transit_station":
                                    case "street_address": //e.g. valendas-sagogn has neither bus_station nor transit_station
                                    case "route": //e.g. Promenada has neither bus_station, transit_station nor street_address
                                        result = r;
                                        break;
                                    default:
                                        break;
                                }
                            }

                            //foreach (XmlNode child in r.ChildNodes)
                            //{
                            //    if (child.Name == "type")
                            //    {
                            //        string typeValue = child.FirstChild.Value;
                            //        if (typeValue == "bus_station")
                            //        {
                            //            result = r;
                            //            break;
                            //        }
                            //        else if (typeValue == "transit_station")
                            //        {
                            //            result = r;
                            //            break;
                            //        }
                            //    }
                            //}


                            //if (firstChild.Name == "type" && firstChild.FirstChild.Value == "bus_station")
                            //{
                            //    result = r;
                            //}
                        }

                        XmlNodeList address_components = result.SelectNodes("address_component");

                        string postal_code = null;
                        string administrative_area_level_2 = null;
                        string administrative_area_level_1 = null;
                        string country = null;

                        foreach (XmlNode address_component in address_components)
                        {
                            string value = address_component.SelectSingleNode("long_name").FirstChild.Value;
                            string type = address_component.SelectSingleNode("type").FirstChild.Value;

                            switch (type)
                            {
                                case "postal_code":
                                    postal_code = value;
                                    break;
                                case "administrative_area_level_2":
                                    administrative_area_level_2 = value;
                                    break;
                                case "administrative_area_level_1":
                                    administrative_area_level_1 = value;
                                    break;
                                case "country":
                                    country = value;
                                    break;
                                default:
                                    // do nothing
                                    break;
                            }
                        }

                        //plausibility check
                        if (postal_code == null || administrative_area_level_2 == null || administrative_area_level_1 == null || country == null)
                        {
                            throw new Exception("Not all necessary data found!");
                        }

                        foreach (Village village in timetableRegion.villages)
                        {
                            if (village.postal_code == postal_code && administrative_area_level_2.Contains(village.administrative_area_level_2)/*manchmal steht da noch "Bezirk"*/ && village.administrative_area_level_1 == administrative_area_level_1 && village.country == country)
                            {
                                ///debug
                                //int stop_name_idx = Array.IndexOf(fieldHeaders, "stop_name");
                                //string stop_name = csv.GetField(stop_name_idx);
                                //Console.WriteLine(stop_id + "  " + stop_name);
                                ///debug

                                stop_ids.Add(stop_id);
                                break;
                            }
                        }
                    }
                }
            }
            return stop_ids;
        }

        private static List<String> getTrip_ids(List<String> stop_ids, string timetablePath)
        {
            List<string> trip_ids = new List<string>();
            
            using (TextReader fileReader = File.OpenText(timetablePath + "\\stop_times.txt"))
            {
                //string value;
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int trip_id_idx = Array.IndexOf(fieldHeaders, "trip_id");
                int stop_id_idx = Array.IndexOf(fieldHeaders, "stop_id");

                //trip_id,arrival_time,departure_time,stop_id,stop_sequence,stop_headsign,pickup_type,drop_off_type,shape_dist_traveled,attributes_ch

                while (csv.Read())
                {
                    string trip_id = csv.GetField(trip_id_idx);
                    string stop_id = csv.GetField(stop_id_idx);

                    if(stop_ids.Contains(stop_id))
                    {
                        if(!trip_ids.Contains(trip_id))
                        {
                            trip_ids.Add(trip_id);
                        }
                    }
                }
            }
            return trip_ids;
        }



        private static List<Trip> getTrips(List<String> trip_ids, string timetablePath)
        {
            List<Trip> trips = new List<Trip>();

            using (TextReader fileReader = File.OpenText(timetablePath + "\\stop_times.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int trip_id_idx = Array.IndexOf(fieldHeaders, "trip_id");
                int arrival_time_idx = Array.IndexOf(fieldHeaders, "arrival_time");
                int departure_time_idx = Array.IndexOf(fieldHeaders, "departure_time");
                int stop_id_idx = Array.IndexOf(fieldHeaders, "stop_id");
                int pickup_type_idx = Array.IndexOf(fieldHeaders, "pickup_type");
                int drop_off_type_idx = Array.IndexOf(fieldHeaders, "drop_off_type");
                //trip_id,arrival_time,departure_time,stop_id,stop_sequence,stop_headsign,pickup_type,drop_off_type,shape_dist_traveled,attributes_ch

                string oldTrip_id = null;
                
                while (csv.Read())
                {
                    string trip_id = csv.GetField(trip_id_idx);

                    int[] temp = Array.ConvertAll(csv.GetField(arrival_time_idx).Split(':'), int.Parse);
                    TimeSpan arrival_time = new TimeSpan(temp[0] / 24, temp[0] % 24, temp[1], temp[2]);
                    temp = Array.ConvertAll(csv.GetField(departure_time_idx).Split(':'), int.Parse);
                    TimeSpan departure_time = new TimeSpan(temp[0] / 24, temp[0] % 24, temp[1], temp[2]);

                    string stop_id = csv.GetField(stop_id_idx);
                    string pickup_type = csv.GetField(pickup_type_idx); 
                    string drop_off_type = csv.GetField(drop_off_type_idx);
                    if (trip_ids.Contains(trip_id))
                    {
                        if (!trip_id.Equals(oldTrip_id))
                        {
                            oldTrip_id = trip_id;
                            trips.Add(new Trip());
                            trips.Last().trip_id = trip_id;
                        }
                        trips.Last().stops.Add(new Stop());
                        trips.Last().stops.Last().trip_id = trips.Last().trip_id;
                        trips.Last().stops.Last().arrival_time = arrival_time;
                        trips.Last().stops.Last().departure_time = departure_time;
                        trips.Last().stops.Last().stop_id = stop_id;
                        trips.Last().stops.Last().pickup_type = pickup_type;
                        trips.Last().stops.Last().drop_off_type = drop_off_type;
                    }
                }
            }
            return trips;
        }

        private static List<Trip> getStop_names(List<Trip> trips, string timetablePath)
        {
            using (TextReader fileReader = File.OpenText(timetablePath + "\\stops.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int stop_id_idx = Array.IndexOf(fieldHeaders, "stop_id");
                int stop_name_idx = Array.IndexOf(fieldHeaders, "stop_name");

                //foreach (Trip trip in trips)
                //{
                //    trip.stop_names = new List<string>(new string[trip.stop_ids.Count]);
                //}

                while (csv.Read())
                {
                    string stop_id = csv.GetField(stop_id_idx);
                    string stop_name = csv.GetField(stop_name_idx);

                    foreach (Trip trip in trips)
                    {
                        //Hier wird das erste mal nach Erstellung der trips durch alle trips iteriert. Es ist nicht schön, das genau hier zu machen, aber praktikabel.
                        trip.stops.First().isFirstStop = true;
                        trip.stops.Last().isLastStop = true;

                        //TODO: if the solution above works then remove the commented part
                        //for (int n = 0; n < trip.stops.Count; n++)
                        //{
                        //    if (trip.stops[n].stop_id.Equals(stop_id))
                        //    {
                        //        trip.stops[n].stop_name = stop_name;
                        //    }
                        //}
                        foreach(Stop stop in trip.stops)
                        {
                            if(stop.stop_id == stop_id)
                            {
                                stop.stop_name = stop_name;
                            }
                        }
                    }
                }
            }
            return trips;
        }

        private static List<Trip> getRoute_id(List<Trip> trips, string timetablePath)
        {
            using (TextReader fileReader = File.OpenText(timetablePath + "\\trips.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int route_id_idx = Array.IndexOf(fieldHeaders, "route_id");
                int service_id_idx = Array.IndexOf(fieldHeaders, "service_id");
                int trip_id_idx = Array.IndexOf(fieldHeaders, "trip_id");
                //int trip_short_name_idx = Array.IndexOf(fieldHeaders, "trip_short_name"); trip_short_name ist nicht zwingend und in diesem Fall nicht vorhanden!!
                 int direction_id_idx = Array.IndexOf(fieldHeaders, "direction_id");
                //route_id,service_id,trip_id,trip_headsign,trip_short_name,direction_id,block_id,shape_id,bikes_allowed,attributes_ch

                while (csv.Read())
                {
                    string route_id = csv.GetField(route_id_idx);
                    string service_id = csv.GetField(service_id_idx);
                    string trip_id = csv.GetField(trip_id_idx);
                    //string trip_short_name = csv.GetField(trip_short_name_idx); trip_short_name ist nicht zwingend und in diesem Fall nicht vorhanden!!
                    //string direction_id = csv.GetField(direction_id_idx); //ist meisten auf einem konstanten Wert. Nach Info von SBB wird dies von einigen ÖV anbietern so gemacht.
                    foreach (Trip trip in trips)
                    {
                        if(trip.trip_id.Equals(trip_id))
                        {
                            trip.route_id = route_id;
                            trip.service_id = service_id;
                            //trip.trip_short_name = trip_short_name;
                            //trip.direction_id = direction_id;
                        }
                    }
                }
            }
            return trips;
        }

        private static List<Trip> getRoute_short_name(List<Trip> trips, string timetablePath)
        {
            using (TextReader fileReader = File.OpenText(timetablePath + "\\routes.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int route_id_idx = Array.IndexOf(fieldHeaders, "route_id");
                int route_short_name_idx = Array.IndexOf(fieldHeaders, "route_short_name");
                //route_id,agency_id,route_short_name,route_long_name,route_desc,route_type,route_url,route_color,route_text_color

                while (csv.Read())
                {
                    string route_id = csv.GetField(route_id_idx);
                    string route_short_name = csv.GetField(route_short_name_idx);
                    foreach (Trip trip in trips)
                    {
                        if (trip.route_id.Equals(route_id))
                        {
                            trip.route_short_name = route_short_name;
                        }
                    }
                }
            }
            return trips;
        }

        private static List<Trip> getActiveDays(List<Trip> trips, string timetablePath)
        {
            //https://developers.google.com/transit/gtfs/reference/calendar-file
            //Each service_id value can appear at most once in a calendar.txt file.

            using (TextReader fileReader = File.OpenText(timetablePath + "\\calendar.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int service_id_idx = Array.IndexOf(fieldHeaders, "service_id");
                int monday_idx = Array.IndexOf(fieldHeaders, "monday");
                int tuesday_idx = Array.IndexOf(fieldHeaders, "tuesday");
                int wednesday_idx = Array.IndexOf(fieldHeaders, "wednesday");
                int thursday_idx = Array.IndexOf(fieldHeaders, "thursday");
                int friday_idx = Array.IndexOf(fieldHeaders, "friday");
                int saturday_idx = Array.IndexOf(fieldHeaders, "saturday");
                int sunday_idx = Array.IndexOf(fieldHeaders, "sunday");
                int start_date_idx = Array.IndexOf(fieldHeaders, "start_date");
                int end_date_idx = Array.IndexOf(fieldHeaders, "end_date");
                //service_id,monday,tuesday,wednesday,thursday,friday,saturday,sunday,start_date,end_date

                while (csv.Read())
                {
                    List<DayOfWeek> activeWeekDays = new List<DayOfWeek>(); 

                    string service_id = csv.GetField(service_id_idx);
                    if (csv.GetField(monday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Monday);
                    if (csv.GetField(tuesday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Tuesday);
                    if (csv.GetField(wednesday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Wednesday);
                    if (csv.GetField(thursday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Thursday);
                    if (csv.GetField(friday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Friday);
                    if (csv.GetField(saturday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Saturday);
                    if (csv.GetField(sunday_idx).Equals("1")) activeWeekDays.Add(DayOfWeek.Sunday);
                    string start_date_string = csv.GetField(start_date_idx);
                    string end_date_string = csv.GetField(end_date_idx);
                    DateTime start_date = (new DateTime(int.Parse(start_date_string.Substring(0, 4)), int.Parse(start_date_string.Substring(4, 2)), int.Parse(start_date_string.Substring(6, 2)))).Date; //make sure no time information is contained as later only date will be compared
                    DateTime end_date = (new DateTime(int.Parse(end_date_string.Substring(0, 4)), int.Parse(end_date_string.Substring(4, 2)), int.Parse(end_date_string.Substring(6, 2)))).Date; //make sure no time information is contained as later only date will be compared

                    foreach (Trip trip in trips)
                    {
                        //geops verwendet nur die id 000000, die über den ganzen Zeitraum gilt.
                        //if (service_id == "000000" || trip.service_id.Equals(service_id))
                        if (trip.service_id.Equals(service_id))
                        {
                            for (DateTime date = start_date; date <= end_date; date = date.AddDays(1))
                            {
                                if (activeWeekDays.Contains(date.DayOfWeek))
                                {
                                    trip.activeDays.Add(date);
                                }
                            }
                        }
                    }
                }
            }

            using (TextReader fileReader = File.OpenText(timetablePath + "\\calendar_dates.txt"))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.IgnoreQuotes = false;

                csv.ReadHeader();
                string[] fieldHeaders = csv.FieldHeaders;
                int service_id_idx = Array.IndexOf(fieldHeaders, "service_id");
                int date_idx = Array.IndexOf(fieldHeaders, "date");
                int exception_type_idx = Array.IndexOf(fieldHeaders, "exception_type");
                //service_id,date,exception_type

                while (csv.Read())
                {
                    string service_id = csv.GetField(service_id_idx);
                    string date_string = csv.GetField(date_idx);
                    DateTime date = new DateTime(int.Parse(date_string.Substring(0, 4)), int.Parse(date_string.Substring(4, 2)), int.Parse(date_string.Substring(6, 2)));
                    //string exception_type = csv.GetField(exception_type_idx);
                    
                    foreach (Trip trip in trips)
                    {
                        if (trip.service_id.Equals(service_id))
                        {
                            if (csv.GetField(exception_type_idx) == "1")
                            {
                                trip.activeDays.Add(date);
                            }
                            else //"2"
                            {
                                //Console.WriteLine(date + "   " + trip.activeDays.IndexOf(date) + "   " + trip.activeDays[trip.activeDays.IndexOf(date)]);
                                if (!trip.activeDays.Remove(date))
                                {
                                    throw (new Exception("zu entfernendes Datum nicht vorhanden"));
                                }
                            }
                        }
                    }
                }
            }
            return trips;
        }

        private static XmlElement googleApisAddressRequest(string address)
        {
            return googleApisRequest("address=" + address);
        }

        private static XmlElement googleApisLatLngRequest(string latlng)
        {
            return googleApisRequest("latlng=" + latlng);
        }

        private static XmlElement googleApisRequest(string requestPart)
        {
            int tryCnt = 0;
            XmlElement documentElement = null;
            string status;

            do
            {
                tryCnt++;

                //https://developers.google.com/maps/premium/previous-licenses/articles/usage-limits?hl=de
                if (tryCnt > 3)
                {
                    System.Threading.Thread.Sleep(2000);
                }

                if(tryCnt > 10)
                {
                    throw new Exception("OVER_QUERY_LIMIT again and again!");
                }

                documentElement = googleApisSingleRequest(requestPart);
                XmlNode statusNode = documentElement.GetElementsByTagName("status").Item(0);
                status = statusNode.FirstChild.Value;
            } while (status == "OVER_QUERY_LIMIT");

            Console.WriteLine(tryCnt);//debug

            return documentElement;
        }

        private static XmlElement googleApisSingleRequest(string requestPart)
        {
            System.Threading.Thread.Sleep(100); //don't do too many requests
            //HttpWebRequest request = HttpWebRequest.CreateHttp("https://maps.googleapis.com/maps/api/geocode/xml?" + requestPart + "&language=en&key=AIzaSyB5x5pX06UrcyVrhxU2osBmGY5wMsIYXoY");//AIzaSyDK7psKXQZKCjHBw-5Frh2WWZd_0Ru4QaA");
            HttpWebRequest request = HttpWebRequest.CreateHttp("https://maps.googleapis.com/maps/api/geocode/xml?" + requestPart + "&language=en");
            WebResponse response = request.GetResponse();

            // Get the stream containing content returned by the server.
            Stream dataStream = response.GetResponseStream();
            // Open the stream using a StreamReader for easy access.
            StreamReader reader = new StreamReader(dataStream);
            // Read the content.
            string responseFromServer = reader.ReadToEnd();

            XmlDocument xmlResponse = new XmlDocument();
            xmlResponse.LoadXml(responseFromServer);

            return xmlResponse.DocumentElement;
        }
    }
}
