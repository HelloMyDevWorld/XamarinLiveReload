# XamarinLiveReload

FULL: https://github.com/klofberg/Xamarin.Forms.Xaml.LiveReload


SHORT
Program.cs -> Console APP
LiveReload.cs -> Add to PCL and use


                  public App()
                       {
                           Xamarin.Forms.Xaml.LiveReload.LiveReload.Enable(this, exception =>
                           {
                               System.Diagnostics.Debug.WriteLine(exception);
                           });

                           SetInitialPage();
                       }
