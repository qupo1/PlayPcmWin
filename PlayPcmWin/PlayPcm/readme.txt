PlayPcm version 1.0.7
Usage:
    PlayPcm
        Print this message and enumerate all available devices

    PlayPcm -d deviceId
        Test specified device

    PlayPcm -d deviceId [-l latencyInMillisec] input_pcm_file_name
        Play pcm file on deviceId device
        Example:
            PlayPcm -d 1 C:\audio\music.wav
            PlayPcm -d 1 C:\audio\music.dsf
            PlayPcm -d 1 C:\audio\music.dff

Changelog

version 1.0.7: Rebuild the program with Visual Studio 2019

version 1.0.6: fix DSDIFF read bug

version 1.0.5: DoP DSDIFF playback

version 1.4: fix error message text

version 1.3: DoP DSF playback for other USB audio

version 1.2: DoP DSF playback for Lynx Hilo

version 1.0: PCM playback
