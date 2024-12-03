using System.Net;
using System.Text;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ProjekatSP2
{
    internal class Program
    {
        private static readonly object cachelock = new object();
        private static string root = "D:\\Elektronski fakultet\\III\\Sistemsko programiranje\\ProjekatSP2\\ProjekatSP2\\Fajlovi";
        private static LRUCache cache = new LRUCache(3);

        static async Task MainAsync()
        {
            HttpListener hListener = new HttpListener();
            hListener.Prefixes.Add("http://127.0.0.1:8080/");
            hListener.Start();
            Console.WriteLine("Server spreman: (http://127.0.0.1:8080/)");
            while (true)
            {
                var c = await hListener.GetContextAsync();
                Task.Run(() => ServerAsync(c));
            }
        }

        static void Main()
        {
            MainAsync();
            Console.ReadLine();
        }


        static async Task SendResponseAsync(HttpListenerContext context, byte[] body, string type = "text/plain; charset=utf-8", HttpStatusCode status = HttpStatusCode.OK)
        {
            HttpListenerResponse response = context.Response;
            response.ContentType = type;
            response.ContentLength64 = body.Length;
            response.StatusCode = (int)status;
            if (type == "application/zip")
            {
                response.AddHeader("Content-Disposition", $"attachment; filename=\"ZipArhiva.zip\"");
            }
            System.IO.Stream output = response.OutputStream;
            await output.WriteAsync(body, 0, body.Length);
            output.Close();
        }


        static async Task ServerAsync(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod != HttpMethod.Get.Method)
                {
                    await SendResponseAsync(context, Encoding.UTF8.GetBytes("\nDozvoljena je samo GET metoda!"), "text/plain; charset=utf-8", HttpStatusCode.BadRequest);
                    return;
                }

                var filenames = context.Request.Url.PathAndQuery.TrimStart('/').Split('&');

                filenames = filenames.Where(file => file != string.Empty).ToArray();

                if (filenames.Length == 0)
                {
                    await SendResponseAsync(context, Encoding.UTF8.GetBytes("\nNije naveden nijedan fajl!"), "text/plain; charset=utf-8", HttpStatusCode.BadRequest);
                    return;
                }

                filenames = filenames.Where(file => File.Exists(Path.Combine(root, file))).ToArray();
                if (filenames.Length == 0)
                {
                    await SendResponseAsync(context, Encoding.UTF8.GetBytes("\nNe postoji nijedan od navedenih fajlova!"), "text/plain; charset=utf-8", HttpStatusCode.NotFound);
                    return;
                }


                await SendResponseAsync(context, ZipArhiva(filenames), "application/zip");
            }
            catch (System.Exception e)
            {
                await SendResponseAsync(context, Encoding.UTF8.GetBytes(e.Message), "text/plain; charset=utf-8", HttpStatusCode.InternalServerError);
            }
        }

        static byte[] ZipArhiva(string[] filenames)
        {
            Array.Sort(filenames, (x, y) => String.Compare(x, y));
            string filenamehash = String.Join(',', filenames);
            try
            {
                if (root == "") root = Environment.CurrentDirectory;

                lock (cachelock)
                {
                    byte[] res;
                    if (cache.checkget(filenamehash))
                    {
                        Console.WriteLine($"\nPronadjen u cache-u: {filenamehash}");
                        res = cache.get(filenamehash);
                        return res;
                    }
                }

                byte[] zipbytes;
                using (MemoryStream mem = new MemoryStream())
                {
                    using (ZipArchive zip = new ZipArchive(mem, ZipArchiveMode.Create))
                    {
                        foreach (string file in filenames)
                        {
                            zip.CreateEntryFromFile(Path.Combine(root, file), file, CompressionLevel.Optimal);
                        }
                    }
                    zipbytes = mem.GetBuffer();
                }

                lock (cachelock)
                {
                    Console.WriteLine($"\nDodat u cache: {filenamehash}");
                    cache.set(filenamehash, zipbytes);
                }

                return zipbytes;
            }
            catch (System.Exception)
            {
                throw;
            }
        }


    }
}