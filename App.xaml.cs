using System.Configuration;
using System.Data;
using System.Windows;

namespace SpeedTestWidget
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize database (backward compatibility)
            DatabaseHelper.InitDatabase();

            // OPTIONAL: Uncomment to clear data on every startup
            // var storage = new SecureStorage();
            // storage.ClearAllData();
            // DatabaseHelper.ClearHistory();
        }
    }
}
