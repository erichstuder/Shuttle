using Newtonsoft.Json;
using PCLExt.FileStorage;
//using PCLStorage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using static Shuttle.Timetable;

namespace Shuttle
{
    //PENDING: name ändern in SearchViewModel
    class SearchViewModel : INotifyPropertyChanged
    {
        //public string Test
        //{
        //    get
        //    {
        //        var a = new List<string>();
        //        a.Add("eins");
        //        a.Add("zweio");
        //        return "uio";
        //    }
        //    //set { }
        //}


        private string launchStopName;
        public string LaunchStopName
        {
            get
            {
                return launchStopName;
            }
            set
            {
                if (value != null)
                {
                    launchStopName = value;
                    saveData("LaunchStopName", value);
                    OnPropertyChanged("LaunchStopName");
                    OnPropertyChanged("PathsText");
                }
            }
        }

        private string targetStopName;
        public string TargetStopName
        {
            get
            {
                return targetStopName;
            }
            set
            {
                if (value != null)
                {
                    targetStopName = value;
                    saveData("TargetStopName", value);
                    OnPropertyChanged("TargetStopName");
                    OnPropertyChanged("PathsText");
                }
            }
        }

        private DateTime launchDate;
        public DateTime LaunchDate
        {
            get
            {
                return launchDate;
            }
            set
            {
                launchDate = value;
                saveData("LaunchDate", value);
                PropertyChangedEventHandler handler = PropertyChanged;
                OnPropertyChanged("PathsText");
            }
        }

        private TimeSpan launchTime;
        public TimeSpan LaunchTime
        {
            get
            {
                return launchTime;
            }
            set
            {
                launchTime = value;
                saveData("LaunchTime", value);
                OnPropertyChanged("PathsText");
            }
        }

        //private TimeSpan stopNameSearchText;
        //public TimeSpan StopNameSearchText
        //{
        //    get
        //    {
        //        return stopNameSearchText;
        //    }
        //    set
        //    {
        //        stopNameSearchText = value;
        //    }
        //}

        public List<Timetable.Path> paths = new List<Timetable.Path>();
        public List<Trip> trips = null;
        private Task initTask = null;

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand SearchCommand { protected set; get; }
        public ICommand UpdateDateAndTimeCommand { protected set; get; }

        public bool searchPending = false;

        public SearchViewModel()
        {
            //saveData("LaunchStopName", "Tessst");
            //string s = loadData<string>("LaunchStopName");


            initTask = Task.Run(() => {
                //JsonSerializerSettings a = new JsonSerializerSettings();
                //a.ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace;
                //aObjectCreationHandling;
                string tripsSerializedJson = null;

                var assembly = typeof(MainPage).GetTypeInfo().Assembly;
                Stream stream = assembly.GetManifestResourceStream("Shuttle.Resources.trips.json");
                using (var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8))
                {
                    tripsSerializedJson = reader.ReadToEnd();
                }

                trips = JsonConvert.DeserializeObject<List<Trip>>(tripsSerializedJson);

                //trips = createGraph(trips);
                //DateTime dummy = DateTime.Now.Date; //dummy execution to be faster above. May no longer be necessary in future
            }).ContinueWith(initTask => {
                if (searchPending)
                {
                    searchPending = false;
                    OnPropertyChanged("PathsText");
                }
            });


            //Task.Run(() =>
            //{
                if (existsData("PathsText"))
                {
                    PathsText = loadData<string>("PathsText");
                }
                else
                {
                    PathsText = "";
                }

                if (existsData("LaunchStopName"))
                {
                    LaunchStopName = loadData<string>("LaunchStopName");
                }

                if (existsData("TargetStopName"))
                {
                    TargetStopName = loadData<string>("TargetStopName");
                }

                if (existsData("LaunchDate"))
                {
                    LaunchDate = loadData<DateTime>("LaunchDate");
                }
                else
                {
                    LaunchDate = DateTime.Now.Date;
                }

                if (existsData("LaunchTime"))
                {
                    LaunchTime = loadData<TimeSpan>("LaunchTime");
                }
                else
                {
                    LaunchTime = DateTime.Now.TimeOfDay;
                }
            //});




            //this.SearchCommand = new Command((o) =>
            //    {
            //        if (!initTask.IsCompleted)
            //        {
            //            initTask.Wait();
            //        }
            //        paths = Timetable.searchPaths(trips, launchDate, launchStopName, targetStopName);
            //        OnPropertyChanged("PathsText"));
            //    });

            this.UpdateDateAndTimeCommand = new Command((o) =>
                {
                    LaunchDate = DateTime.Now.Date;
                    OnPropertyChanged("LaunchDate");

                    LaunchTime = DateTime.Now.TimeOfDay;
                    OnPropertyChanged("LaunchTime");
                });


            //    void SearchCommand()
            //{
            //        if (!initTask.IsCompleted)
            //        {
            //            initTask.Wait();
            //        }
            //        paths = Timetable.searchPaths(trips, DateTime.Now.Date, LaunchStopName, targetStopName);
            //        OnPropertyChanged("PathsText"));
            //    }


            //new System.Threading.Thread(new System.Threading.ThreadStart(() =>
            //{
            //    //JsonSerializerSettings a = new JsonSerializerSettings();
            //    //a.ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace;
            //    //aObjectCreationHandling;
            //    string tripsSerializedJson;
            //    using (StreamReader sr = new StreamReader(this.Assets.Open("trips.json")))
            //    {
            //        tripsSerializedJson = sr.ReadToEnd();
            //    }
            //    trips = JsonConvert.DeserializeObject<List<Trip>>(tripsSerializedJson);

            //    trips = createGraph(trips);
            //    DateTime dummy = DateTime.Now.Date; //dummy execution to be faster above. May no longer be necessary in future
            //    loadingDone = true;
            //})).Start();
        }

        //void OnLaunchSelected(object sender, SelectedItemChangedEventArgs e)
        //{
        //    if (e.SelectedItem != null) //ItemSelected is called on deselection, which results in SelectedItem being set to null
        //    {
        //        LaunchStopName = e.SelectedItem.ToString();
        //    }
        //}

        //void OnTargetSelected(object sender, SelectedItemChangedEventArgs e)
        //{
        //    if (e.SelectedItem != null) //ItemSelected is called on deselection, which results in SelectedItem being set to null
        //    {
        //        targetStopName = e.SelectedItem.ToString();
        //    }
        //}

        private string pathsText = "";
        public string PathsText
        {
            set
            {
                pathsText = value;
            }

            get
            {
                if (!initTask.IsCompleted)
                {
                    searchPending = true;
                    return pathsText;

                    //initTask.Wait();
                }
                paths = Timetable.searchPaths(trips, LaunchDate, LaunchStopName, TargetStopName);

                string searchPathResult = "";
                if (paths.Count == 0)
                {
                    //if (existsData("PathsText"))
                    //{
                    //    searchPathResult = loadData<string>("PathsText");
                    //}
                    //else
                    //{
                        searchPathResult = "keine Verbindung gefunden";
                    //}
                }
                else
                {
                    //searchPathResult = "Verbindungen am " + LaunchDate.ToString("dd. MMMM yyyy") + " um " + LaunchTime.ToString(@"hh\:mm") + "\n\n";
                    //searchPathResult = "Verbindungen:\n\n";

                    for (int n = paths.Count - 1; n >= 0; n--)
                    {
                        Timetable.Path path = paths[n];

                        if(path.stops.First().departure_time < LaunchTime.Subtract(new TimeSpan(0,5,0))) //show buses aswell that have just left. Maybe they are late...
                        {
                            continue;
                        }

                        int nrOfStops = path.stops.Count;
                        if (nrOfStops > 1)
                        {
                            searchPathResult += "ab: " + path.stops[0].departure_time.ToString(@"hh\:mm") + "  " + path.stops[0].stop_name + "\n";
                            for (int stopIdx = 0; stopIdx < path.stops.Count - 1; stopIdx++)//exclude last as it has no next stop
                            {
                                Stop actualStop = path.stops[stopIdx];
                                Stop nextStop = path.stops[stopIdx + 1];
                                if (actualStop.trip_id != nextStop.trip_id)
                                {
                                    searchPathResult += "an: " + actualStop.arrival_time.ToString(@"hh\:mm") + "  " + actualStop.stop_name + "\n";
                                    searchPathResult += "=> umsteigen\n";
                                    searchPathResult += "ab: " + nextStop.departure_time.ToString(@"hh\:mm") + "  " + nextStop.stop_name + "\n";
                                }

                            }
                        }
                        searchPathResult += "an: " + path.stops[nrOfStops - 1].arrival_time.ToString(@"hh\:mm") + "  " + path.stops[nrOfStops - 1].stop_name + "\n";
                        searchPathResult += "\n";
                    }
                }
                //Application.Current.Properties["PathsText"] = searchPathResult;
                saveData("PathsText", searchPathResult);
                return searchPathResult;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if(handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        public static void saveData(string name, object objectToSerialize)
        {
            string serialized_Json = JsonConvert.SerializeObject(objectToSerialize);

            IFolder folder = getMyLocalFolder();

            IFile file = folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting).Result;

            file.WriteAllTextAsync(serialized_Json).Wait();
        }

        public static T loadData<T>(string name)
        {
            IFolder folder = getMyLocalFolder();

            IFile file = folder.GetFileAsync(name).Result;

            string serialized_Json = file.ReadAllTextAsync().Result;

            return JsonConvert.DeserializeObject<T>(serialized_Json);
        }

        public static bool existsData(string name)
        {
            IFolder folder = getMyLocalFolder();

            ExistenceCheckResult x = folder.CheckExistsAsync(name).Result;
            return x == ExistenceCheckResult.FileExists;
        }

        private static IFolder getMyLocalFolder()
        {
            string myFolderName = "Shuttle";
            IFolder localStorageFolder = FileSystem.LocalStorage;
            return localStorageFolder.CreateFolderAsync(myFolderName, CreationCollisionOption.OpenIfExists).Result;
        }

    }
}
