// 日本語UTF-8

using WWMFReaderCs;
using WWUtil;

namespace WWSoundFileRW {

    class Mp3Reader {
        public string path;
        public LargeArray<byte> data;
        public WWMFReader.Metadata meta;

        public int Read(string path) {
            this.path = path;

            int hr = WWMFReader.ReadHeaderAndData(path, out meta, out data);
            if (hr < 0) {
                return hr;
            }

            return 0;
        }

        public void ReadStreamEnd() {
            data = null;
        }
    };
}
