using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Shuttle.Timetable;

namespace Timetable_Printer
{
    class Program
    {
        private class RoutePrintInfo
        {
            public readonly List<ActivePeriod> activePeriods;
            public readonly List<string> routeDefinition;
            public readonly int tripsPerPage;

            public readonly List<Trip> trips;

            public RoutePrintInfo(List<ActivePeriod> activePeriods, List<string> routeDefinition, int tripsPerPage, List<Trip> trips)
            {
                this.activePeriods = activePeriods;
                this.routeDefinition = routeDefinition;
                this.tripsPerPage = tripsPerPage;
                this.trips = trips;
            }
        }

        private class TimetablePrintInfo
        {
            public readonly string regionInfo;
            public readonly List<RoutePrintInfo> routePrintInfos;

            public TimetablePrintInfo(string regionInfo, List<RoutePrintInfo> routePrintInfos)
            {
                this.regionInfo = regionInfo;
                this.routePrintInfos = routePrintInfos;
            }
        }

        private class ActivePeriod
        {
            public readonly DateTime startDate;
            public readonly DateTime endDate;

            public ActivePeriod(DateTime startDate, DateTime endDate)
            {
                this.startDate = startDate;
                this.endDate = endDate;
            }
        }

        static void Main(string[] args)
        {
            printTimetable();
            Console.ReadLine();
            return;
        }

        private static void printTimetable()
        {
            //string json = File.ReadAllText("..\\..\\..\\Timetable_Manager\\trips_geops.json");
            //string json = File.ReadAllText("..\\..\\..\\Timetable_Manager\\trips_transitfeeds.json");
            string json = File.ReadAllText("..\\..\\..\\Timetable_Manager\\trips.json");

            List<Trip> trips = JsonConvert.DeserializeObject<List<Trip>>(json);


            //trips = createGraph(trips);// nicht unbedingt notwendig

            sortTrips(trips);


            XDocument timetablePrintInfosXML = XDocument.Load("..\\..\\timetablePrintInfos.xml");

            List<TimetablePrintInfo> timetablePrintInfos = new List<TimetablePrintInfo>();

            foreach (XElement timetablePrintInfo in timetablePrintInfosXML.Root.Elements("timetablePrintInfo"))
            {
                string regionInfo = timetablePrintInfo.Element("regionInfo").Value;

                List<RoutePrintInfo> routePrintInfos = new List<RoutePrintInfo>();

                foreach (XElement routePrintInfo in timetablePrintInfo.Elements("routePrintInfo"))
                {
                    List<ActivePeriod> activePeriods = new List<ActivePeriod>();
                    foreach (XElement activePeriod in routePrintInfo.Elements("activePeriod"))
                    {
                        DateTime startDate = DateTime.Parse(activePeriod.Element("startDate").Value);
                        DateTime endDate = DateTime.Parse(activePeriod.Element("endDate").Value);

                        activePeriods.Add(new ActivePeriod(startDate, endDate));
                    }

                    int tripsPerPage_forward = int.Parse(routePrintInfo.Element("forward").Element("tripsPerPage").Value);
                    int tripsPerPage_backward = int.Parse(routePrintInfo.Element("backward").Element("tripsPerPage").Value);

                    List<string> routeDefinition = new List<string>(routePrintInfo.Element("routeDefinition").Value.Trim().Split('\n').Select(s => s.Trim()));

                    List<Trip> tripsList_forward = new List<Trip>();
                    List<Trip> tripsList_backward = new List<Trip>();
                    foreach (Trip trip in trips)
                    {
                        if (!tripIsInActivePeriod(trip, activePeriods))
                        {
                            continue;
                        }

                        //check if trip fits into route definition
                        int tripStopsCnt_forward = 0;
                        int tripStopsCnt_backward = trip.stops.Count - 1;
                        int routeStopsCnt = 0;

                        List<Stop> tripStops = trip.stops;
                        do
                        {
                            if (tripStops[tripStopsCnt_forward].stop_name == routeDefinition[routeStopsCnt])
                            {
                                tripStopsCnt_forward++;
                            }

                            if (tripStops[tripStopsCnt_backward].stop_name == routeDefinition[routeStopsCnt])
                            {
                                tripStopsCnt_backward--;
                            }

                            routeStopsCnt++;

                            if (tripStopsCnt_forward >= tripStops.Count) //trip fits to route (forward)
                            {
                                tripsList_forward.Add(trip);
                                break;
                            }
                            else if (tripStopsCnt_backward < 0) //trip fits to route (backward)
                            {
                                tripsList_backward.Add(trip);
                                break;
                            }
                            else if (routeStopsCnt >= routeDefinition.Count) //trip does NOT fit to route
                            {
                                break;
                            }
                        } while (true);
                    }

                    routePrintInfos.Add(new RoutePrintInfo(activePeriods, routeDefinition, tripsPerPage_forward, tripsList_forward));

                    var routeDefinition_backward = new List<string>(routeDefinition);
                    routeDefinition_backward.Reverse();
                    routePrintInfos.Add(new RoutePrintInfo(activePeriods, routeDefinition_backward, tripsPerPage_backward, tripsList_backward));
                }
                timetablePrintInfos.Add(new TimetablePrintInfo(regionInfo, routePrintInfos));
            }

            //print
            foreach (TimetablePrintInfo timetablePrintInfo in timetablePrintInfos)
            {
                foreach (RoutePrintInfo routePrintInfo in timetablePrintInfo.routePrintInfos)
                {
                    //print region info
                    Console.WriteLine(timetablePrintInfo.regionInfo);

                    //print active periods info
                    string activePeriodText = "active period:";
                    foreach (ActivePeriod activePeriod in routePrintInfo.activePeriods)
                    {
                        activePeriodText += activePeriod.startDate.ToString("dd.MM.yyyy") + " bis " + activePeriod.endDate.ToString("dd.MM.yyyy") + "\n";
                    }
                    Console.Write(activePeriodText);

                    //prepare list with stop names for print
                    int maxStopNameLength = 0;
                    foreach (string stopName in routePrintInfo.routeDefinition)
                    {
                        maxStopNameLength = Math.Max(maxStopNameLength, stopName.Length);
                    }
                    List<string> nameLines = new List<string>(routePrintInfo.routeDefinition);
                    for (int n = 0; n < nameLines.Count; n++)
                    {
                        nameLines[n] = nameLines[n].PadRight(maxStopNameLength);
                    }


                    List<string> timeLines = new List<string>(Enumerable.Repeat("", routePrintInfo.routeDefinition.Count).ToList());


                    for (int tripCnt = 0; tripCnt < routePrintInfo.trips.Count; tripCnt++)
                    {
                        Trip trip = routePrintInfo.trips[tripCnt];


                        // check if any weekdays do not occure
                        List<DateTime> activeDays = trip.activeDays;
                        bool hasMonday    = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Monday);
                        bool hasTuesday   = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Tuesday);
                        bool hasWednesday = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Wednesday);
                        bool hasThursday  = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Thursday);
                        bool hasFriday    = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Friday);
                        bool hasSaturday  = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Saturday);
                        bool hasSunday    = hasDayOfWeek(activeDays, routePrintInfo.activePeriods, DayOfWeek.Sunday);
                        
                        Console.Write("no: ");
                        if (!hasMonday)    { Console.Write("MO,"); }
                        if (!hasTuesday)   { Console.Write("TU,"); }
                        if (!hasWednesday) { Console.Write("WE,"); }
                        if (!hasThursday)  { Console.Write("TH,"); }
                        if (!hasFriday)    { Console.Write("FR,"); }
                        if (!hasSaturday)  { Console.Write("SA,"); }
                        if (!hasSunday)    { Console.Write("SU,"); }


                        //check if a trip is not active a the beginning of an active period
                        foreach (ActivePeriod activePeriod in routePrintInfo.activePeriods)
                        {
                            if (activePeriod.startDate < activeDays.Min())
                            {
                                DateTime secondDate = activeDays.Min() < activePeriod.endDate ? activeDays.Min().Subtract(new TimeSpan(1, 0, 0, 0)) : activePeriod.endDate; //get the first one
                                Console.Write(activePeriod.startDate.ToString("dd.MM.yyyy") + "to" + secondDate.ToString("dd.MM.yyyy") + ", ");
                            }
                        }
                        foreach (ActivePeriod activePeriod in routePrintInfo.activePeriods)
                        {
                            //check if a trip has other none-active days which are not already excluded by week-days
                            DateTime localStartDate = activePeriod.startDate > activeDays.Min() ? activePeriod.startDate : activeDays.Min();
                            DateTime localEndDate = activePeriod.endDate < activeDays.Max() ? activePeriod.endDate : activeDays.Max();
                            for (DateTime day = localStartDate; day <= localEndDate; day = day.Add(new TimeSpan(1, 0, 0, 0)))
                            {
                                if (!activeDays.Contains(day))
                                {
                                    if ((day.DayOfWeek == DayOfWeek.Monday && hasMonday) ||
                                        (day.DayOfWeek == DayOfWeek.Tuesday && hasTuesday) ||
                                        (day.DayOfWeek == DayOfWeek.Wednesday && hasWednesday) ||
                                        (day.DayOfWeek == DayOfWeek.Thursday && hasThursday) ||
                                        (day.DayOfWeek == DayOfWeek.Friday && hasFriday) ||
                                        (day.DayOfWeek == DayOfWeek.Saturday && hasSaturday) ||
                                        (day.DayOfWeek == DayOfWeek.Sunday && hasSunday))
                                    {
                                        Console.Write(day.ToString("dd.MM.yyyy") + ", ");
                                    }

                                }
                            }
                        }

                        //check if a trip is not active a the end of an active period
                        foreach (ActivePeriod activePeriod in routePrintInfo.activePeriods)
                        {
                            //DateTime maxDay = days.Max();
                            if (activePeriod.endDate > activeDays.Max())
                            {
                                DateTime firstDate = activeDays.Max() > activePeriod.startDate ? activeDays.Max().Add(new TimeSpan(1, 0, 0, 0)) : activePeriod.startDate; //get the first one
                                Console.Write(firstDate.ToString("dd.MM.yyyy") + "to" + activePeriod.endDate.ToString("dd.MM.yyyy") + ", ");
                            }
                        }

                        Console.WriteLine("");

                        //print the times
                        int routeCnt = 0;
                        for (int n = 0; n < trip.stops.Count; n++)
                        {
                            Stop stop = trip.stops[n];
                            while (routePrintInfo.routeDefinition[routeCnt] != stop.stop_name)
                            {
                                timeLines[routeCnt] += "       ";
                                routeCnt++;
                            }

                            if (stop.noPickupAvailable())
                            {
                                timeLines[routeCnt] += " @";
                            }
                            else
                            {
                                timeLines[routeCnt] += "  ";
                            }

                            timeLines[routeCnt] += stop.departure_time.ToString(@"hh\:mm");//TODO: beim letzten evtl. die arrival Time angeben
                            routeCnt++;
                        }
                        while (routeCnt < routePrintInfo.routeDefinition.Count)
                        {
                            timeLines[routeCnt] += "       ";
                            routeCnt++;
                        }


                        //print when tripsPerPage is reached or at the end
                        if ((tripCnt > 0 && ((tripCnt+1) % routePrintInfo.tripsPerPage == 0)) || tripCnt == routePrintInfo.trips.Count-1)
                        {
                            for (int lineCnt = 0; lineCnt < timeLines.Count; lineCnt++)
                            {
                                Console.WriteLine(nameLines[lineCnt] + "  " + timeLines[lineCnt]);
                            }
                            Console.WriteLine("");

                            timeLines = new List<string>(Enumerable.Repeat("", routePrintInfo.routeDefinition.Count).ToList());
                        }
                    }
                }
            }



            // check if all trips have been printed
            List<Trip> printedTrips = new List<Trip>();
            foreach (TimetablePrintInfo timetablePrintInfo in timetablePrintInfos)
            {
                foreach (RoutePrintInfo routePrintInfo in timetablePrintInfo.routePrintInfos)
                {
                    printedTrips.AddRange(routePrintInfo.trips);
                }
            }
            foreach (Trip trip in trips)
            {
                DateTime startDate = new DateTime(2017, 04, 18);
                //DateTime startDate = new DateTime(2016, 12, 11);
                DateTime endDate = new DateTime(2017, 12, 1);
                List<DateTime> days = new List<DateTime>(trip.activeDays);
                for (int n = days.Count - 1; n >= 0; n--)
                {
                    DateTime day = days[n];
                    if (day < startDate || day > endDate)
                    {
                        days.Remove(day);
                    }
                }

                if (!printedTrips.Contains(trip) && days.Count > 0)
                {
                    //Console.WriteLine(trip.route_id);
                    throw new Exception("not all trips are printed!");
                }
            }


            return;
        }


        private static void sortTrips(List<Trip> trips)
        {
            //trips sortieren
            //sortierung: vom zeitlich ersten zum zeitlich letzten
            bool sortFinished;
            do
            {
                sortFinished = true;
                for (int n = 1; n < trips.Count; n++)
                {
                    double meanA = 0;
                    int cntA = 0;
                    foreach (Stop stop in trips[n - 1].stops)
                    {
                        if (stop != null)
                        {
                            meanA += stop.departure_time.TotalMinutes;
                            cntA++;
                        }
                    }
                    meanA /= cntA;

                    double meanB = 0;
                    int cntB = 0;
                    foreach (Stop stop in trips[n].stops)
                    {
                        if (stop != null)
                        {
                            meanB += stop.departure_time.TotalMinutes;
                            cntB++;
                        }
                    }
                    meanB /= cntB;

                    if (meanA > meanB)
                    {
                        Trip temp = trips[n - 1];
                        trips[n - 1] = trips[n];
                        trips[n] = temp;
                        sortFinished = false;
                    }
                }
            } while (!sortFinished);
        }


        private static bool tripIsInActivePeriod(Trip trip, List<ActivePeriod> activePeriods)
        {
            //check if trip fits into an activePeriod
            foreach (ActivePeriod activePeriod in activePeriods)
            {
                DateTime startDate = activePeriod.startDate;
                DateTime endDate = activePeriod.endDate;

                foreach (DateTime activeDay in trip.activeDays)
                {
                    if (activeDay >= startDate && activeDay <= endDate)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        private static bool hasDayOfWeek(List<DateTime> activeDays, List<ActivePeriod> activePeriods, DayOfWeek dayOfWeek)
        {
            foreach (DateTime day in activeDays)
            {
                // check if day lies within any of the active periods
                bool isWithin = false;
                foreach (ActivePeriod activePeriod in activePeriods)
                {
                    if (day >= activePeriod.startDate && day <= activePeriod.endDate)
                    {
                        isWithin = true;
                        break;
                    }
                }
                if(isWithin && day.DayOfWeek == dayOfWeek)
                {
                    return true;
                }
            }
            return false;
        }

    }
}
