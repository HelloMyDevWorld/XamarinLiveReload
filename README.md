# XamarinLiveReload

FULL: https://github.com/klofberg/Xamarin.Forms.Xaml.LiveReload

Program -> Console APP
LiveReload -> Add to PCL and use

                  public App()
                       {
                           Xamarin.Forms.Xaml.LiveReload.LiveReload.Enable(this, exception =>
                           {
                               System.Diagnostics.Debug.WriteLine(exception);
                           });

                           SetInitialPage();
                       }
