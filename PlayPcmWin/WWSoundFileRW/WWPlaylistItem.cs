namespace WWSoundFileRW {
    public class WWPlaylistItem {
        public string Title { get; set; }
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string ComposerName { get; set; }
        public string PathName { get; set; }
        public int CueSheetIndex { get; set; }
        public int StartTick { get; set; }
        public int EndTick { get; set; }
        public bool ReadSeparaterAfter { get; set; }
        public int TrackId { get; set; }
        public long LastWriteTime { get; set; }
    }
}
