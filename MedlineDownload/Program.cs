using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.IO.Compression;

namespace MedlineDownload
{
    static class Program
    {
        public class ArticleInfos
        {
            public string PMID { set; get; }
            public string[] KeyWords { set; get; }
            public string DocumentType { set; get; }
        }


        private const int MAX_DOWNLOAD_TASKS = 16;
        private static string XMLDir = "xml";
        
        private static string FtpUrl = "ftp://ftp.ncbi.nlm.nih.gov/pubmed/baseline/";




        public static void Main()
        {
            string tmpConsoleResult = "";
            Console.Write("XML-Dir (default: " + XMLDir + ") empty = default:");
            tmpConsoleResult = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(tmpConsoleResult))
            {
                XMLDir = tmpConsoleResult;
            }

            if(!Directory.Exists(XMLDir))
            {
                Directory.CreateDirectory(XMLDir);
            }

            Console.Write("download files? (yes, now):");

            tmpConsoleResult = Console.ReadLine();
            if(tmpConsoleResult.StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {               

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Clear files? (yes, no):");
                Console.ForegroundColor = ConsoleColor.Gray;
                tmpConsoleResult = Console.ReadLine();
                if (tmpConsoleResult.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var file in Directory.GetFiles(XMLDir))
                    {
                        File.Delete(file);
                    }
                    Console.WriteLine("folder cleaned!");
                }

                Console.Write("Download-Url (default: " + FtpUrl + ") empty = default:");
                tmpConsoleResult = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(tmpConsoleResult))
                {
                    FtpUrl = tmpConsoleResult;
                }
                Console.WriteLine("starting download...");
                DownloadFiles();
            }


            var files = System.IO.Directory.GetFiles(XMLDir, "*.xml*");
            
            foreach(var file in files)
            {
                GetArticleInfosFromFile(file);

            }




            Console.ReadLine();
        }


        private static void GetArticleInfosFromFile(string file, bool zipped = true)
        {
            var xmlDocument = new System.Xml.XmlDocument();
            if (zipped)
            {
                using (var fileStream = System.IO.File.OpenRead(file))
                using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    xmlDocument.Load(gzipStream);
                }
            }
            else
            {
                xmlDocument.Load(file);
            }

            var articles = xmlDocument.SelectNodes("/PubmedArticleSet/PubmedArticle/MedlineCitation");
            foreach(System.Xml.XmlNode article in articles)
            {
                var pmid =  article.SelectSingleNode("PMID").InnerText;

                var keywords = article.SelectNodes("KeywordList/Keyword");

                var publicationTypes = article.SelectNodes("Article/PublicationTypeList/PublicationType");

                Console.WriteLine("PMID: " + pmid);
                if(keywords.Count >= 1) { Console.WriteLine("Keywords: "); }                
                foreach(System.Xml.XmlNode keyword in keywords)
                {
                    Console.WriteLine("\t" + keyword.InnerText);
                }

                
                if (publicationTypes.Count >= 1) { Console.WriteLine("Publication Types: "); }
                foreach (System.Xml.XmlNode publiccationType in publicationTypes)
                {
                    Console.WriteLine("\t" + publiccationType.InnerText);
                }
                Console.WriteLine();
            }

            xmlDocument = null;
            GC.Collect();
        }







        private static void DownloadFiles()
        {
            //get overview
            var baseRequest = (FtpWebRequest)WebRequest.Create(FtpUrl);
            baseRequest.Method = WebRequestMethods.Ftp.ListDirectory;
            var baseResponse = (FtpWebResponse) baseRequest.GetResponse();


            var baseResponseStream = baseResponse.GetResponseStream();
                       
            var baseReader = new StreamReader(baseResponseStream);
            
            string fileNamesText = baseReader.ReadToEnd();
            
            //split on every new line format
            var fileNames = fileNamesText.Split( new[] { "\r\n", "\r", "\n" },StringSplitOptions.None);

            Console.WriteLine("downloaded list, found " + fileNames.Length + " files");

            Parallel.ForEach(fileNames, new ParallelOptions() { MaxDegreeOfParallelism = MAX_DOWNLOAD_TASKS },
                fileName =>
                {
                    DownloadFile(fileName);
                });
            Console.WriteLine("download finished");
              
        }


        private static void DownloadFile(string fileName, bool unzip = false)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.EndsWith(".md5", StringComparison.OrdinalIgnoreCase)) return;
            

            string ioFileName = Path.Combine(XMLDir, fileName);
            if (unzip && fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) )
            {
                ioFileName = ioFileName.Remove(ioFileName.Length - 3);
            }
            if (File.Exists(ioFileName)) return;



            Console.WriteLine("Downloading File: " + fileName);

            var fileRequest = (FtpWebRequest)WebRequest.Create(FtpUrl + fileName);

            fileRequest.Method = WebRequestMethods.Ftp.DownloadFile;
            var fileResponse = (FtpWebResponse)fileRequest.GetResponse();

            if(unzip && fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                using (var fileReader = fileResponse.GetResponseStream())
                using (var ioFileStream = System.IO.File.OpenWrite(ioFileName))
                using (var decompressionStream = new GZipStream(fileReader, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(ioFileStream);
                }
            }
            else
            {
                using (var fileReader = fileResponse.GetResponseStream())
                using (var ioFileStream = System.IO.File.OpenWrite(ioFileName))
                {
                    fileReader.CopyTo(ioFileStream);
                }
            }




        }




    }
}
