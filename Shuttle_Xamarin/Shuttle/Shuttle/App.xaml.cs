using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xamarin.Forms;

namespace Shuttle
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            //MainPage = new Shuttle.MainPage();
            

            MainPage = new NavigationPage(new Shuttle.MainPage());
            //NavigationPage.SetHasNavigationBar(this, false);
            //NavigationPage.SetHasBackButton(this, false);
        }

        protected override void OnStart()
        {
            // Handle when your app starts

            const string EulaText = "Niemand ist perfekt und so kann auch diese App Fehler enthalten.\n" +
                                    "Bitte beachte: Für jegliche Schäden wird die Haftung abgelehnt.\n" +
                                    "\n" +
                                    "So, das musste mal gesagt werden.\n" +
                                    "Nun aber viel Spass mit dieser App!";

            const string EULA_ACCEPTED_ID = "EulaAccepted";

            if (!SearchViewModel.existsData(EULA_ACCEPTED_ID))
            {
                //System.Threading.Tasks.Task task = new System.Threading.Tasks.Task();
                //task.Run

                //System.Threading.Tasks.Task<bool> task = this.MainPage.DisplayAlert("Wichtige Information", EulaText, "akzeptieren", "nicht mit mir");
                //task.Wait();
                //bool accepted = task.Result;
                //if (accepted)
                //{
                //    int a = 2;
                //}
                //var This = this;

                //this.MainPage.DisplayActionSheet("title", "cancel", "destruction", new string[] { "a", "b" });

                System.Threading.Tasks.Task.Run(async () =>
                {
                    System.Threading.Tasks.Task<bool> task;
                    bool accepted = false;

                    while (!accepted)
                    {
                        task = null;
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            task = this.MainPage.DisplayAlert("Wichtige Information", EulaText, "ok", "nicht einverstanden");
                        });

                        while (task == null)
                        {
                            await System.Threading.Tasks.Task.Delay(500);
                        }

                        task.Wait();
                        accepted = task.Result;
                    }

                    SearchViewModel.saveData(EULA_ACCEPTED_ID, "is accepted");

                });


            }
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
