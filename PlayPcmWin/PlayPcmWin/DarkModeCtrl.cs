using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace PlayPcmWin {
    class DarkModeCtrl {

        public bool IsDarkMode() {
            string RegistryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            var o = Registry.GetValue(RegistryKey, "AppsUseLightTheme", 1);
            if (o == null) {
                return false;
            }
            
            var v = o as int?;
            if (v == null) {
                return false;
            }

            return v == 0 ? true : false;
        }
    }
}
