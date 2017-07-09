using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xamarin.Forms;

namespace Shuttle
{
    public static class Timetable
    {
        //public static Binding StopNamesProperty
        //private static List<string> stopNames = null;
        public static List<string> StopNames
        {
            get;
        } = initializeStopNames();

        private static List<string> initializeStopNames()
        {
            string stop_namesSerialized_Json = null;
            var assembly = typeof(MainPage).GetTypeInfo().Assembly;
            Stream stream = assembly.GetManifestResourceStream("Shuttle.Resources.stop_names.json");

            using (var reader = new System.IO.StreamReader(stream))
            {
                stop_namesSerialized_Json = reader.ReadToEnd();
            }

            List<string> stop_names = JsonConvert.DeserializeObject<List<string>>(stop_namesSerialized_Json);

            return stop_names;
        }

        public class Path
        {
            public List<Stop> stops = new List<Stop>();
            public double timeSpanSeconds = 0;
        }

        public class Stop
        {
            //[JsonIgnore]
            public Trip trip; //used to find out what active days a stop has (circular)

            public string trip_id;
            public List<Stop> nextStops = new List<Stop>();

            //from stop_times.txt
            public TimeSpan arrival_time;
            public TimeSpan departure_time;
            public string stop_id;
            //stop_sequences //wird nicht ausgewertet, da davon ausgegangen wird, dass die reihenholge im file korrekt ist.
            //stop_headsign //wird nicht verwendet
            public string pickup_type;
            public string drop_off_type;
            //any further fields are not available or unused

            //from stops.txt
            public string stop_name;

            //indicates whether this stop is the first or the last stop of a trip
            public bool isFirstStop = false;
            public bool isLastStop = false;


            //0: Regularly scheduled pickup
            //1: No pickup available
            //2: Must phone agency to arrange pickup
            //3: Must coordinate with driver to arrange pickup
            public bool regularyScheduledPickup() { return this.pickup_type == "0"; }
            public bool noPickupAvailable() { return this.pickup_type == "1"; }
            public bool mustPhoneAgencyToArrangePickup() { return this.pickup_type == "2"; }
            public bool mustCoordinateWithDriverToArrangePickup() { return this.pickup_type == "3"; }

            //0: Regularly scheduled drop off
            //1: No drop off available
            //2: Must phone agency to arrange drop off
            //3: Must coordinate with driver to arrange drop off
            //private enum DropOffTypes
            //{
            //    string RegularlyScheduledDropOff = "0";
            //}
            public bool regularlyScheduledDropOff() { return this.drop_off_type == "0"; }
            public bool noDropOffAvailable() { return this.drop_off_type == "1"; }
            public bool mustPhoneAgencyToArrangeDropOff() { return this.drop_off_type == "2"; }
            public bool mustCoordinateWithDriverToArrangeDropOff() { return this.drop_off_type == "3"; }

        }

        public class Trip
        {
            public List<Stop> stops = new List<Stop>();

            //from stop_times.txt
            public string trip_id;

            //from trips.txt
            public String route_id;
            public String service_id;
            //public String trip_short_name; //nicht zwingend vorhanden
            //public String direction_id; //ist meisten auf einem konstanten Wert. Nach Info von SBB wird dies von einigen ÖV anbietern so gemacht.

            //from routes.txt
            public String route_short_name;

            //from calendar.txt
            //public String start_date;
            //public String end_date;
            public List<DateTime> activeDays = new List<DateTime>();
        }

        public static List<Trip> createGraph(List<Trip> trips)
        {
            //die einzelnen Trips intern verknüpfen
            foreach (Trip trip in trips)
            {
                int n = 0;
                for (; n < trip.stops.Count - 1; n++)
                {
                    trip.stops[n].nextStops.Add(trip.stops[n + 1]);
                }
            }

            foreach (Trip a_trip in trips)
            {
                for (int a = 0; a < a_trip.stops.Count; a++)
                {
                    Stop a_stop = a_trip.stops[a];
                    a_stop.trip = a_trip;////////////////////////könnte man evtl. auch anderswo machen
                    string a_stop_name = a_stop.stop_name;
                    TimeSpan a_stop_arrival_time = a_stop.arrival_time;

                    string aBefore_stop_name;
                    if (a > 0)
                    {
                        aBefore_stop_name = a_trip.stops[a - 1].stop_name;
                    }
                    else
                    {
                        aBefore_stop_name = null;
                    }

                    string aNext_stop_name;
                    bool aNext_noDropOff;
                    if (a < a_trip.stops.Count - 1)
                    {
                        aNext_stop_name = a_trip.stops[a + 1].stop_name;
                        aNext_noDropOff = a_trip.stops[a + 1].drop_off_type == "1";//PENDING: methode aus objekt verwenden
                    }
                    else
                    {
                        aNext_stop_name = null;
                        aNext_noDropOff = true;
                    }


                    foreach (Trip b_trip in trips)
                    {
                        for (int b = 0; b < b_trip.stops.Count; b++)
                        {
                            Stop b_stop = b_trip.stops[b];
                            string b_stop_name = b_stop.stop_name;
                            TimeSpan b_stop_departure_time = b_stop.departure_time;

                            string bNext_stop_name;
                            bool bNext_noPickup;
                            if (b < b_trip.stops.Count - 1)
                            {
                                bNext_stop_name = b_trip.stops[b + 1].stop_name;
                                bNext_noPickup = b_trip.stops[b + 1].pickup_type == "1";//PENDING: methode aus objekt verwenden
                            }
                            else
                            {
                                bNext_stop_name = null;
                                bNext_noPickup = true;
                            }

                            //if (a_stop_name == b_stop_name && a_stop_arrival_time <= b_stop_departure_time && !a_stop.noDropOffAvailable() && !b_stop.noPickupAvailable()) //Möglichkeit zum Umsteigen gefunden
                            //{
                            //    if (aNext_stop_name != bNext_stop_name && aBefore_stop_name != bNext_stop_name && aBefore_stop_name != null &&  bNext_stop_name != null)
                            //    {
                            //        a_stop.nextStops.Add(b_stop);
                            //    }
                            //}


                            if (a_trip.trip_id != b_trip.trip_id                                                // connections within the trip have already been done above
                                && a_stop_name == b_stop_name                                                   // stops have the same name
                                && a_stop_arrival_time <= b_stop_departure_time                                 // times match
                                && !a_stop.noDropOffAvailable()                                                 // drop off is available
                                && !b_stop.noPickupAvailable()                                                  // pickup is avialable
                                && (aNext_stop_name != bNext_stop_name || aNext_noDropOff || bNext_noPickup)    // change only if next stations don't have the same name or it can't be changed at next stations. 
                                && (aBefore_stop_name != bNext_stop_name || bNext_noPickup)                     // change only if you don't drive back except you can't enter the stop when driving back.
                                && !a_stop.isFirstStop                                                          // changing right at the beginning of a trip makes no sense
                                && !b_stop.isLastStop                                                           // changing to the end of a trip makes no sense
                                )                                                  
                            {
                                a_stop.nextStops.Add(b_stop);
                            }
                        }
                    }
                }
            }
            return trips;
        }

        private static List<Stop> getLaunchStops(List<Trip> trips, DateTime launchDateTime, string launchStop_name)
        {
            List<Stop> launchStops = new List<Stop>();
            
            foreach (Trip trip in trips)
            {
                if (trip.activeDays.Contains(launchDateTime.Date)) //trip has to be active on the desired date
                {
                    foreach (Stop stop in trip.stops)
                    {
                        if (stop.stop_name == launchStop_name && !stop.isLastStop && !stop.noPickupAvailable())
                        {
                            launchStops.Add(stop);
                        }
                    }
                }
            }
            return launchStops;
        }

        private static List<Stop> getTargetStops(List<Trip> trips, DateTime launchDateTime, string targetStop_name)
        {
            List<Stop> targetStops = new List<Stop>();

            foreach (Trip trip in trips)
            {
                if (trip.activeDays.Contains(launchDateTime.Date)) //trip has to be active on the desired date
                {
                    foreach (Stop stop in trip.stops)
                    {
                        if (stop.stop_name == targetStop_name && !stop.isFirstStop && !stop.noDropOffAvailable())
                        {
                            targetStops.Add(stop);
                        }
                    }
                }
            }
            return targetStops;
        }

        private static Path getTimelyShortestPath(List<Path> paths)
        {
            //find timely shortest path
            Path actualPath = null;
            //TimeSpan actualTimeSpan = actualPath.stops.Last().arrival_time - actualPath.stops.First().departure_time;
            double actualTimeSpanSeconds = Double.MaxValue;
            foreach (Path path in paths)
            {
                //TimeSpan arrival_time = path.stops.Last().arrival_time;
                //TimeSpan departure_time = path.stops.First().departure_time;
                //TimeSpan newTimeSpan = path.stops.Last().arrival_time - path.stops.First().departure_time;
                double newTimeSpanSeconds = path.timeSpanSeconds;
                if (newTimeSpanSeconds < actualTimeSpanSeconds)
                {
                    actualTimeSpanSeconds = newTimeSpanSeconds;
                    actualPath = path;
                    //if(actualTimeSpanSeconds == 0) //nicht sicher, ob das was bringt. Wohl eher zu Beginn der Suche.
                    //{
                    //    break; //kleiner als 0 ist nicht möglich. Somit ist der kürzeste gefunden.
                    //}
                }
            }
            return actualPath;
        }

        private static Path searchSinglePath(List<Stop> launchStops, string targetStop_name, List<string> usedTrip_ids, TimeSpan latestArrivalTime, DateTime launchDateTime)
        {
            //if (launchStops == null || launchStops.Count == 0)
            //{
            //    return null;
            //}

            //initialize
            //int pathsCountMax = 0;//bebug
            List<string> localUsedTrip_ids = new List<string>();
            //Path foundPath = null;
            Path actualPath = new Path();
            //actualPath.stops.Add(launchStop);
            List<Path> paths = new List<Path>();
            foreach (Stop launchStop in launchStops)
            {
                Path path = new Path();
                path.stops.Add(launchStop);
                path.timeSpanSeconds = (path.stops.Last().arrival_time - path.stops.First().departure_time).TotalSeconds;
                paths.Add(path);
            }

            //search path
            do
            {
                //debug
                //if(paths.Count > pathsCountMax)
                //{
                //    pathsCountMax = paths.Count;
                //}
                //debug



                if (paths.Count > 0) //wenn noch mögliche Pfade vorhanden sind, dann weiter machen, sonst ist fertig
                {
                    actualPath = getTimelyShortestPath(paths);
                    paths.Remove(actualPath);
                }
                else
                {
                    usedTrip_ids.AddRange(localUsedTrip_ids);

                    //System.Diagnostics.Debug.WriteLine(pathsCountMax); //debug
                    return null;
                }

                if (actualPath.stops.Last().stop_name == targetStop_name)
                {
                    usedTrip_ids.AddRange(localUsedTrip_ids);

                    //System.Diagnostics.Debug.WriteLine(pathsCountMax); //debug
                    return actualPath;
                }

                foreach (Stop stop in actualPath.stops.Last().nextStops)
                {
                    //debug
                    //int n = 0;
                    //if (stop.stop_name == "Falera, Parcadi" && stop.arrival_time == new TimeSpan(13,15,00))
                    //{ n++; }
                    //debug

                    bool stationAlreadyPresent = false;
                    int nrOfElements = actualPath.stops.Count;
                    if (nrOfElements >= 2)
                    {
                        //a stop must not arise more than 2 times in a row
                        if(actualPath.stops[nrOfElements - 1].stop_id == stop.stop_id && actualPath.stops[nrOfElements - 2].stop_id == stop.stop_id)
                        {
                            stationAlreadyPresent = true;
                        }
                    }

                    //check if the station already exists except the last one (Umsteigen)
                    for(int n = 0; n< actualPath.stops.Count-1; n++ )
                    {
                        Stop actualStop = actualPath.stops[n];
                        //if (actualStop.stop_id == stop.stop_id && !actualStop.noDropOffAvailable() && !stop.noPickupAvailable())
                        if (actualStop.stop_id == stop.stop_id && !(actualStop.noDropOffAvailable() || stop.noPickupAvailable()))
                        {
                                stationAlreadyPresent = true;
                        }
                    }

                    //for (int n = actualPath.stops.Count - 2; n >= 0; n--)//Ausser der letzten Haltestelle, darf die neue nicht schon vorkommen (Umsteigen).
                    //{
                    //    if (actualPath.stops[n].stop_id == stop.stop_id)
                    //    {
                    //        stationAlreadyPresent = true;
                    //        break;
                    //    }
                    //}
                    
                    if (!stationAlreadyPresent && !usedTrip_ids.Contains(stop.trip_id) && stop.arrival_time <= latestArrivalTime && stop.trip.activeDays.Contains(launchDateTime))
                    {
                        //Jeder trip, der bei einer suchen gefunden wird, kann nicht bei einer früheren Abfahrtszeit keine Rolle mehr spielen.
                        //Jeder trip, der bei einer Suche gefunden wird, ist entweder teil des resultates oder führt nicht optimal oder gar nicht zum Ziel.
                        if (!localUsedTrip_ids.Contains(stop.trip_id))
                        {
                            localUsedTrip_ids.Add(stop.trip_id);
                        }
                        Path path = new Path();
                        path.stops = new List<Stop>(actualPath.stops);
                        path.stops.Add(stop);
                        //while (path.stops.Last().nextStops.Count == 1 && path.stops.Last().stop_name != targetStop_name)//alle nächsten Stationen, bei denen nicht umgestiegen werden kann werden gleich mit angehängt.
                        //{
                        //    //Achtung: es darf natürlich nicht über die gesuchte station hinaus hinzugefügt werden!!!
                        //    path.stops.Add(path.stops.Last().nextStops.Single());
                        //}

                        //if (path.stops.Last().arrival_time <= latestArrivalTime) //Prüfung nur notwendig, wenn etra stationen hinzugefügt werden (siehe gleich davor)
                        //{
                            //calcualte time span
                            path.timeSpanSeconds = (path.stops.Last().arrival_time - path.stops.First().departure_time).TotalSeconds;

                            paths.Add(path);
                        //}
                    }
                }
            }
            while (true);
        }

        public static List<Path> searchPaths(List<Trip> trips, DateTime launchDateTime, string launchStop_name, string targetStop_name)
        {
            List<Stop> launchStops = getLaunchStops(trips, launchDateTime, launchStop_name);
            List<Stop> targetStops = getTargetStops(trips, launchDateTime, targetStop_name);
            List<string> usedTrip_ids = new List<string>();
            List<Path> paths = new List<Path>();

            //sort
            bool sortFinished;
            do
            {
                sortFinished = true;
                for (int n = 0; n < launchStops.Count-1; n++)
                {
                    if(launchStops[n].departure_time < launchStops[n+1].departure_time) //latest first
                    {
                        Stop temp = launchStops[n];
                        launchStops[n] = launchStops[n + 1];
                        launchStops[n + 1] = temp;
                        sortFinished = false;
                    }
                }
            } while (!sortFinished);

            for (int n = 0; n<launchStops.Count; )
            {
                List<Stop> relevantLaunchStops = new List<Stop>();
                relevantLaunchStops.Add(launchStops[n]);
                n++;
                while(n < launchStops.Count)//there could be more than one launch stop with same departure_time
                {
                    if (relevantLaunchStops.First().departure_time == launchStops[n].departure_time)
                    {
                        relevantLaunchStops.Add(launchStops[n]);
                        n++;
                    }
                    else
                    {
                        break;
                    }
                }

                //check for latest arrival time an if path is possible due to possible arrivals
                TimeSpan latestArrival_time = TimeSpan.MinValue;
                bool pathIsPossible = false;
                foreach(Stop targetStop in targetStops)
                {
                    if(targetStop.arrival_time > latestArrival_time)
                    {
                        latestArrival_time = targetStop.arrival_time;
                    }

                    if(targetStop.arrival_time >= relevantLaunchStops.First().arrival_time)
                    {
                        pathIsPossible = true;
                    }
                }

                Path path;
                if (pathIsPossible)
                {
                    path = searchSinglePath(relevantLaunchStops, targetStop_name, usedTrip_ids, latestArrival_time, launchDateTime);
                }
                else
                {
                    path = null;
                }

                
                if (path != null)
                {
                    paths.Add(path);

                    TimeSpan arrivalTime = path.stops.Last().arrival_time;
                    for (int m=targetStops.Count-1; m>=0; m--)
                    {
                        Stop targetStop = targetStops[m];
                        if(targetStop.arrival_time >= arrivalTime)
                        {
                            targetStops.Remove(targetStop);
                        }
                    }
                }
            }

            return paths;
        }

        //private static List<Path> searchAllPaths(List<Stop> launchStops, string targetStop_name)
        //{
        //    if(launchStops == null || launchStops.Count == 0)
        //    {
        //        return null;
        //    }

        //    //initialize
        //    List<string> usedTrip_ids = new List<string>();
        //    List<Path> foundPaths = new List<Path>();
        //    Path actualPath = new Path();
        //    //actualPath.stops.Add(launchStop);
        //    List<Path> paths = new List<Path>();
        //    foreach (Stop stop in launchStops)
        //    {
        //        Path path = new Path();
        //        path.stops.Add(stop);
        //        paths.Add(path);
        //    }

        //    //search path
        //    do
        //    {
        //        if (paths.Count > 0) //wenn noch mögliche Pfade vorhanden sind, dann weiter machen, sonst ist fertig
        //        {
        //            actualPath = getTimelyShortestPath(paths);
        //            paths.Remove(actualPath);
        //        }
        //        else
        //        {
        //            return foundPaths;
        //        }

        //        if (actualPath.stops.Last().stop_name == targetStop_name)
        //        {
        //            foundPaths.Add(actualPath);
        //            for(int n = paths.Count-1; n>=0; n--)
        //            {
        //                //remove all paths with same start time. This works because it makes no sense anymore to start at this time.
        //                Path path = paths[n];
        //                if(path.stops.First().departure_time.CompareTo(actualPath.stops.First().departure_time) == 0 )
        //                {
        //                    paths.Remove(path);
        //                }
        //            }

        //            foreach(Stop stop in actualPath.stops)
        //            {
        //                string trip_id = stop.trip_id;
        //                if (!usedTrip_ids.Contains(trip_id))
        //                {
        //                    usedTrip_ids.Add(trip_id);
        //                }
        //            }

        //            for(int n = paths.Count - 1; n>=0; n--)
        //            {
        //                Path path = paths[n];
        //                foreach (Stop stop in path.stops)
        //                {
        //                    if(usedTrip_ids.Contains(stop.trip_id))
        //                    {
        //                        paths.Remove(path);
        //                        break;
        //                    }
        //                }
        //            }


        //            if (paths.Count > 0) //wenn noch mögliche Pfade vorhanden sind, dann weiter machen, sonst ist fertig
        //            {
        //                actualPath = getTimelyShortestPath(paths);
        //                paths.Remove(actualPath);
        //            }
        //            else
        //            {
        //                return foundPaths;
        //            }
        //        }



        //        foreach (Stop stop in actualPath.stops.Last().nextStops)
        //        {
        //            Path path = new Path();
        //            path.stops = new List<Stop>(actualPath.stops);
        //            bool stationAlreadyPresent = false;
        //            for(int n=actualPath.stops.Count-2; n>=0; n--)//Ausser der letzten Haltestelle, darf die neue nicht schon vorkommen (Umsteigen).
        //            {
        //                if(actualPath.stops[n].stop_id == stop.stop_id)
        //                {
        //                    stationAlreadyPresent = true;
        //                    break;
        //                }
        //            }

        //            if (!stationAlreadyPresent && path.stops.First().stop_id != stop.stop_id && !usedTrip_ids.Contains(stop.trip_id))
        //            {
        //                path.stops.Add(stop);
        //                while (path.stops.Last().nextStops.Count == 1)//alle nächsten Stationen, bei denen nicht umgestiegen werden kann werden gleich mit angehängt.
        //                {
        //                    path.stops.Add(path.stops.Last().nextStops.Single());
        //                }
        //                paths.Add(path);
        //            }
        //        }
        //    }
        //    while (true);
        //}

        //public static List<Path> searchPaths(List<Trip> trips, DateTime launchDateTime, string launchStop_name, string targetStop_name)
        //{
        //    List<Stop> launchStops = getLaunchStops(trips, launchDateTime, launchStop_name);

        //    List<Path> paths = searchAllPaths(launchStops, targetStop_name);

        //    return paths;
        //}

        public static List<string> getAllStopNames(List<Trip> trips)
        {
            List<string> stop_names = new List<string>();
            foreach (Trip trip in trips)
            {
                foreach (Stop stop in trip.stops)
                {
                    string stop_name = stop.stop_name;
                    if (!stop_names.Contains(stop_name))
                    {
                        stop_names.Add(stop_name);
                    }
                }
            }
            return stop_names;
        }
    }
}
