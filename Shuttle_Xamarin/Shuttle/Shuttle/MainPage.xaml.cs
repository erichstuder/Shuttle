using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using static Shuttle.Timetable;

namespace Shuttle
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void Button_LaunchStation_Clicked(object sender, EventArgs e)
        {
            startStationSelectPage(sender, true);
        }

        private void Button_TargetStation_Clicked(object sender, EventArgs e)
        {
            startStationSelectPage(sender, false);
        }

        private async void startStationSelectPage(object sender, bool isLaunchStation)
        {
            if (!StationSelectPage.IsActive)//prevent from opening a second one
            {
                //Der BindingContext muss gesetzt werden, damit der Text auf dem Button von der zweiten Page aus gesetzt werden kann.
                Button button = (Button)sender;

                var stationSelectPage = new StationSelectPage(isLaunchStation);
                stationSelectPage.BindingContext = button.BindingContext;

                await Navigation.PushModalAsync(stationSelectPage);
            }
        }
    }


}
