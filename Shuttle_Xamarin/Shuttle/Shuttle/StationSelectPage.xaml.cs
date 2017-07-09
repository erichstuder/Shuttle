using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Shuttle
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class StationSelectPage : ContentPage
    {
        public bool isLaunchStation; //otherwise target station
        private const string FavoriteStations_ID = "FavoriteStations";
        private static bool isActive = false;

        public static bool IsActive
        {
            get
            {
                return isActive;
            }
        }

        public StationSelectPage(bool isLaunchStation)
        {
            InitializeComponent();
            this.isLaunchStation = isLaunchStation;

            if (!SearchViewModel.existsData(FavoriteStations_ID))
            {
                SearchViewModel.saveData(FavoriteStations_ID, new List<string>());
            }

            //var itemsList = getFullItemsList();
            var itemsList = SearchViewModel.loadData<List<string>>(FavoriteStations_ID);
            setItemsSource(ListView_SearchedStationNames, itemsList);
            //ListView_SearchedStationNames.ItemsSource = getFullItemsList();

            var template = new DataTemplate(typeof(TextCell));
            template.SetValue(TextCell.TextColorProperty, Color.Black);
            template.SetBinding(TextCell.TextProperty, "Text");
            ListView_SearchedStationNames.ItemTemplate = template;
        }


        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            List<string> favoriteStations = SearchViewModel.loadData<List<string>>(FavoriteStations_ID);

            string[] fragments = e.NewTextValue.Split(' ');

            //Die Favoriten sollen nur angezeigt werden, wenn kein Suchtext eingegeben ist.
            List<string> newItemsList;
            if (e.NewTextValue.Length == 0)
            {
                //newItemsList = getFullItemsList();
                newItemsList = SearchViewModel.loadData<List<string>>(FavoriteStations_ID);
            }
            else if(e.NewTextValue == "*")
            {
                newItemsList = Timetable.StopNames;
            }
            else
            {
                newItemsList = new List<string>();

                foreach (string stopName in Timetable.StopNames)
                {
                    //if no fragments are available, then all stations are shown.
                    bool containsAll = true;
                    foreach (string fragment in fragments)
                    {
                        if (!stopName.ToLower().Contains(fragment.ToLower()))
                        {
                            containsAll = false;
                            break;
                        }
                    }
                    if (containsAll)
                    {
                        newItemsList.Add(stopName);
                    }
                }
            }
            setItemsSource(ListView_SearchedStationNames, newItemsList);
            //ListView_SearchedStationNames.ItemsSource = newItemsList;
        }

        //private List<string> getFullItemsList()
        //{
        //    List<string> favoriteStations = SearchViewModel.loadData<List<string>>(FavoriteStations_ID);
        //    List<string> stopNames = Timetable.StopNames;

        //    //// workaround to prevent problems when a stop names changes e.g. after an updated => TODO schöner machen
        //    for (int n = favoriteStations.Count - 1; n >= 0; n--)
        //    {
        //        string favoriteStation = favoriteStations[n];
        //        if (!stopNames.Contains(favoriteStation))
        //        {
        //            favoriteStations.Remove(favoriteStation);
        //        }
        //    }
        //    SearchViewModel.saveData(FavoriteStations_ID, favoriteStations);
        //    ////

        //    List<string> itemsList = favoriteStations.GetRange(0, Math.Min(10, favoriteStations.Count));
        //    if (itemsList.Count > 0)
        //    {
        //        itemsList.Add("");
        //    }
        //    itemsList.AddRange(stopNames);



        //    return itemsList;
        //}

        private async void ListView_SearchedStationNames_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            ListView listView = (ListView)sender;
            List<string> favoriteStations;
            string stopName = ((MyTextItem)listView.SelectedItem).Text;

            if (stopName != "")
            {
                favoriteStations = SearchViewModel.loadData<List<string>>(FavoriteStations_ID);
                favoriteStations.Remove(stopName);
                favoriteStations.Insert(0, stopName);

                SearchViewModel.saveData(FavoriteStations_ID, favoriteStations);
            }
            
            if (isLaunchStation)
            {
                ((SearchViewModel)listView.BindingContext).LaunchStopName = stopName;
            }
            else
            {
                ((SearchViewModel)listView.BindingContext).TargetStopName = stopName;
            }
            await Navigation.PopModalAsync();
        }

        private void setItemsSource(ListView listView, List<string> itemsList)
        {
            ObservableCollection<MyTextItem> collection = new ObservableCollection<MyTextItem>();
            for (int n = 0; n < itemsList.Count; n++)
            {
                collection.Add(new MyTextItem { Text = itemsList[n] });
            }
            ListView_SearchedStationNames.ItemsSource = collection;
        }

        public class MyTextItem
        {
            public string Text { get; set; }
        }

        //protected override void OnBindingContextChanged()
        //{
        //    this.OnDisapearing
        //    var a = this.IsFocused;
        //    var b = this.IsVisible;
        //    var c = this.IsEnabled;
        //    var d = this.IsBusy;
        //    StationSelectPage.isActive = false;
        //}

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StationSelectPage.isActive = false;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StationSelectPage.isActive = true;
        }
    }
}
