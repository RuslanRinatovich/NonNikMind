using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindKeeper.Entity
{
    public partial class File
    {
        public string FileIcon
        {
            get
            {
                if (string.IsNullOrEmpty(FileType)) return "File";
                switch (FileType.ToLower())
                {
                    case "pdf": return "FilePdf";
                    case "jpg": case "jpeg": case "png": case "gif": return "FileImage";
                    case "doc": case "docx": return "FileWord";
                    case "xls": case "xlsx": return "FileExcel";
                    case "txt": return "FileDocument";
                    default: return "File";
                }
            }
        }
    }
}
