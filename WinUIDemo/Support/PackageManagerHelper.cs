using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace WinUIDemo
{
	public static class PackageManagerHelper
	{
		public static bool IteratePackages()
		{
			Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
			try
			{
				IEnumerable<Windows.ApplicationModel.Package> packages = (IEnumerable<Windows.ApplicationModel.Package>)packageManager.FindPackages();

				int packageCount = 0;
				foreach (var package in packages)
				{
					Debug.WriteLine(new string('=', 60));
					DisplayPackageInfo(package);
					DisplayPackageUsers(packageManager, package);
					packageCount += 1;
				}

				Debug.WriteLine(new string('=', 60));

				if (packageCount == 0)
					Debug.WriteLine($"No packages were found.");
				else
					Debug.WriteLine($"Total packages: {packageCount}");

			}
			catch (UnauthorizedAccessException)
			{
				Debug.WriteLine($"PackageManagerHelper.IteratePackages() failed because access was denied. This must be run elevated.");

				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(string.Format("PackageManagerHelper.IteratePackages() failed, error message: {0}", ex.Message));
				Debug.WriteLine(string.Format("{0}", ex.ToString()));

				return false;
			}

			return true;
		}

        public static List<Windows.ApplicationModel.Package> GatherPackages()
        {
			List<Windows.ApplicationModel.Package> packageList = new List<Package>();
            Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
            try
            {
                IEnumerable<Windows.ApplicationModel.Package> packages = (IEnumerable<Windows.ApplicationModel.Package>)packageManager.FindPackages();

                foreach (var package in packages)
                {
					if (package != null)
						packageList.Add(package);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine($"PackageManagerHelper.GatherPackages() failed because access was denied. This must be run elevated.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("PackageManagerHelper.GatherPackages() failed, error message: {0}", ex.Message));
            }
            return packageList;
        }

        #region [Supporting Methods]
        public static void DisplayPackageUsers(Windows.Management.Deployment.PackageManager packageManager, Windows.ApplicationModel.Package package)
		{
			StringBuilder sb = new StringBuilder();
			try
			{
				IEnumerable<Windows.Management.Deployment.PackageUserInformation> packageUsers = packageManager.FindUsers(package.Id.FullName);

				sb.Append("Users: ");
				// Normally there will be only one user, unless running from a networked multi-user account terminal.
				foreach (var packageUser in packageUsers)
				{
					sb.Append(string.Format("{0}, ", SidToAccountName(packageUser.UserSecurityId)));
				}

				Debug.WriteLine(sb.ToString());
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DisplayPackageUsers: {ex.Message}");
			}
		}

		public static string SidToAccountName(string sidString)
		{
			System.Security.Principal.SecurityIdentifier sid = new System.Security.Principal.SecurityIdentifier(sidString);
			try
			{
				System.Security.Principal.NTAccount account = (System.Security.Principal.NTAccount)sid.Translate(typeof(System.Security.Principal.NTAccount));
				return account.ToString();
			}
			catch (System.Security.Principal.IdentityNotMappedException)
			{
				return sidString;
			}
		}

		public static void DisplayPackageInfo(Windows.ApplicationModel.Package package)
		{
			try
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(string.Format("Name: {0}", package.Id.Name));
				sb.AppendLine(string.Format("FullName: {0}", package.Id.FullName));
				sb.AppendLine(string.Format("Version: {0}.{1}.{2}.{3}", package.Id.Version.Major, package.Id.Version.Minor, package.Id.Version.Build, package.Id.Version.Revision));
				sb.AppendLine(string.Format("Publisher: {0}", package.Id.Publisher));
				sb.AppendLine(string.Format("PublisherId: {0}", package.Id.PublisherId));
				sb.AppendLine(string.Format("Installed Location: {0}", package.InstalledLocation.Path));
				//sb.AppendLine(string.Format("Architecture: {0}", Enum.GetName(typeof(Windows.Management.Deployment.PackageArchitecture), package.Id.Architecture)));
				sb.AppendLine(string.Format("IsFramework: {0}", package.IsFramework));

				Debug.Write(sb.ToString());
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"DisplayPackageInfo: {ex.Message}");
			}
		}
		#endregion
	}
}
