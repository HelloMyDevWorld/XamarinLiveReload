# XamarinLiveReload

# Usage
                  public App()
                       {
                           Xamarin.Forms.Xaml.LiveReload.LiveReload.Enable(this, exception =>
                           {
                               System.Diagnostics.Debug.WriteLine(exception);
                           });

                           SetInitialPage();
                       }

Author of idea: https://github.com/klofberg/Xamarin.Forms.Xaml.LiveReload
