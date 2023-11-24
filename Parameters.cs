using System.Collections.Generic;

namespace webs
{
    public class Parameters
    {
        public List<string> errors = new List<string>();
        internal string rootFolder;
        internal bool runAsAdmin = false;
        internal bool ignoreConfig = false;
        internal bool installContextMenu = false;
        internal bool addPath = false;
        internal bool uninstallContextMenu = false;
        internal bool removePath = false;
        internal bool noBrowser = false;
        internal bool https = false;
        internal int HTTP_port = 8080;
        internal bool defaultHTTP = true;
        internal int HTTPS_port = 4443;
        internal bool defaultHTTPS = true;
    }
}
