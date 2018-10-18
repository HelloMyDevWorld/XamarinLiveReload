//USE

      public App()
        {
            LiveReload.Enable(this, exception =>
            {
                System.Diagnostics.Debug.WriteLine(exception);
            });

            InitializeComponent();
        }

// URL to your project where you have .sln 