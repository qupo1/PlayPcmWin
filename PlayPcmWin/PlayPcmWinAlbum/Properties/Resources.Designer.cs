﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PlayPcmWinAlbum.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("PlayPcmWinAlbum.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to FLAC file is not found on {0}.
        ///Press Yes to pecify another folder.
        ///Press No to exit this app..
        /// </summary>
        internal static string ErrorMusicFileNotFound {
            get {
                return ResourceManager.GetString("ErrorMusicFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error: Playback start failed! Error code = 0x{0:X8}.
        /// </summary>
        internal static string ErrorPlaybackStartFailed {
            get {
                return ResourceManager.GetString("ErrorPlaybackStartFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Float.
        /// </summary>
        internal static string FloatingPointNumbers {
            get {
                return ResourceManager.GetString("FloatingPointNumbers", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Counting Files {0} ....
        /// </summary>
        internal static string LogCountingFiles {
            get {
                return ResourceManager.GetString("LogCountingFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Creating Music Album List ({0}%) ....
        /// </summary>
        internal static string LogCreatingMusicList {
            get {
                return ResourceManager.GetString("LogCreatingMusicList", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found {0} files..
        /// </summary>
        internal static string LogReportCount {
            get {
                return ResourceManager.GetString("LogReportCount", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Playback Control.
        /// </summary>
        internal static string MainGroupBoxPlaybackControl {
            get {
                return ResourceManager.GetString("MainGroupBoxPlaybackControl", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Playback Device.
        /// </summary>
        internal static string MainGroupBoxPlaybackDevice {
            get {
                return ResourceManager.GetString("MainGroupBoxPlaybackDevice", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to WASAPI Settings.
        /// </summary>
        internal static string MainGroupBoxWasapiSettings {
            get {
                return ResourceManager.GetString("MainGroupBoxWasapiSettings", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Paused..
        /// </summary>
        internal static string MainStatusPaused {
            get {
                return ResourceManager.GetString("MainStatusPaused", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Playing. PCM {0}Hz {1} {2}ch .
        /// </summary>
        internal static string MainStatusPlaying {
            get {
                return ResourceManager.GetString("MainStatusPlaying", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reading PCM from file and arranging decoded PCM to main memory....
        /// </summary>
        internal static string MainStatusReadingFiles {
            get {
                return ResourceManager.GetString("MainStatusReadingFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Playback stopped..
        /// </summary>
        internal static string MainStatusStopped {
            get {
                return ResourceManager.GetString("MainStatusStopped", resourceCulture);
            }
        }
    }
}
