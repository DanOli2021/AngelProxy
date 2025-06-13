using AngelDB;
using AngelProxy;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting.WindowsServices;
using Newtonsoft.Json;
using System.Data;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

//if is a Windows service, set the current directory to the same as the executable
if (WindowsServiceHelpers.IsWindowsService())
{
    Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
}

//Console.WriteLine(Environment.CurrentDirectory);
string commandLine = string.Join(" ", args);
string app_directory = Environment.CurrentDirectory + "/scripts";

if (!string.IsNullOrEmpty(commandLine))
{

    DbLanguage language = new DbLanguage();
    language.SetCommands(AngelSQL.ProxyCommands.DbCommands());
    Dictionary<string, string> d = new Dictionary<string, string>();

    d = language.Interpreter(commandLine);

    switch (d.First().Key)
    {
        case "app_directory":
            app_directory = d["app_directory"];
            break;
        default:
            LogFile.Log($"Error: Invalid command line argument {d.First().Key}");
            Environment.Exit(0);
            return;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Database
var db = new AngelDB.DB();
string result = db.Prompt($"SCRIPT FILE {app_directory}/Start.csx");
db.Prompt($"VAR db_scripts_directory = '" + app_directory + "'");

if (result.StartsWith("Error:"))
{
    LogFile.Log(result);
    return;
}

HostData hostData = new HostData();
db.CreateTable(hostData, "HostData");

AngelDB.MemoryDb mem = new MemoryDb();
mem.ConnectionString = "Data Source=memory;Cache=Shared;Mode=ReadWriteCreate;Version=3;Pooling=true;Max Pool Size=1000;";
mem.SQLExec("CREATE TABLE IF NOT EXISTS HostData (Id TEXT PRIMARY KEY, Account TEXT, Branch TEXT, Timestamp TEXT, Host TEXT, Active NUMBER)");
mem.SQLExec("CREATE INDEX IF NOT EXISTS idx_account ON HostData (Account, branch)");
mem.SQLExec("CREATE TABLE IF NOT EXISTS idx_timestamp ON HostData (Timestamp)");
mem.SQLExec("CREATE TABLE IF NOT EXISTS idx_active ON HostData (Active)");

mem.SQLExec("CREATE TABLE IF NOT EXISTS hostarrays (Id TEXT PRIMARY KEY, account TEXT, branch TEXT, points TEXT, active NUMBER, resources TEXT, Timestamp TEXT)");
mem.SQLExec("CREATE INDEX IF NOT EXISTS idx_account_branch ON hostarrays (account, branch)");

_ = Task.Run(() =>
{
    try
    {

        while (true)
        {
            DataTable mem_table = mem.SQLTable("SELECT * FROM hostdata ORDER BY timestamp DESC LIMIT 1");
            string last_timestamp = "";

            if (mem_table.Rows.Count > 0)
            {
                DataRow row = mem_table.Rows[0];
                last_timestamp = row["Timestamp"].ToString();
            }
            else
            {
                last_timestamp = "1900-01-01 01:01:01";
            }

            string result = db.Prompt("SELECT * FROM hostdata WHERE active = 1 AND timestamp > '" + last_timestamp + "'");

            DataTable dt = db.jSonDeserialize<DataTable>(result);

            foreach (DataRow r in dt.Rows)
            {
                mem.Reset();
                mem.CreateInsert("hostdata");
                mem.AddField("Id", r["Id"].ToString());
                mem.AddField("Account", r["Account"].ToString());
                mem.AddField("Branch", r["Branch"].ToString());
                mem.AddField("Host", r["Host"].ToString());
                mem.AddField("Active", Convert.ToInt32(r["Active"]));
                mem.AddField("Timestamp", r["timestamp"].ToString());
                result = mem.Exec();

                if( result.StartsWith("Error:"))
                {
                    LogFile.Log($"Error: inserting host data: {result}");
                }

            }

            // Espera 5 segundos antes de volver a consultar la base de datos
            Thread.Sleep(5000);
        }
    }
    catch (Exception e)
    {
        LogFile.Log($"Error: initializing memory database: {e.Message}");
    }
});

builder.WebHost.ConfigureKestrel(options =>
{

    List<Certificates> certificates = JsonConvert.DeserializeObject<List<Certificates>>(result);

    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(new HttpsConnectionAdapterOptions
        {
            ServerCertificateSelector = (context, domain) =>
            {
                // Selecciona el certificado en función del dominio solicitado
                var certInfo = certificates.FirstOrDefault(c => c.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));

                if (certInfo != null)
                {
                    try
                    {
                        if (!File.Exists(certInfo.Certificate))
                        {
                            return null;
                        }

                        return new X509Certificate2(certInfo.Certificate, certInfo.Password);
                    }
                    catch (Exception e)
                    {
                        LogFile.Log($"Error loading certificate for domain {certInfo.Domain}: {e.Message}");
                        return null;
                    }
                }

                return null;
            }
        });
    });
});


var app = builder.Build();

var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(10) // Por ejemplo, 10 minutos
};

// Ruta que captura todas las solicitudes
app.Map("{**catch-all}", (Delegate)(async (HttpContext context) =>
{
    var requestMessage = new HttpRequestMessage();

    // Copia el método HTTP
    requestMessage.Method = new HttpMethod(context.Request.Method);

    string requestDomain = context.Request.Headers["Host"].ToString();
    string customer = UrlHelper.ExtractClient(context.Request.Path);

    if (string.IsNullOrEmpty(customer))
    {
        customer = requestDomain.Split('.')[0]; // Extrae el cliente del dominio si no se encuentra en la URL
    }

    customer = customer.ToLowerInvariant();

    DataTable mem_table = mem.SQLTable($"SELECT * FROM hostdata WHERE Id = '{customer}'");

    if (mem_table.Rows.Count == 0)
    {
        LogFile.Log($"Error: Customer {customer} not found in memory database.");
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Account and branch not found.");
        return;
    }

    Uri uri;

    result = mem_table.Rows[0]["Host"].ToString();

    List<string> parts = [.. result.Split(',')];

    string host = mem_table.Rows[0]["Host"].ToString();

    if (parts.Count > 0)
    {
        DataTable dataTable = mem.SQLTable($"SELECT * FROM hostarrays WHERE account = '{mem_table.Rows[0]["Account"]}' AND branch = {mem_table.Rows[0]["Branch"]} AND active = 1 ORDER BY points DESC LIMIT 1");

        if (dataTable.Rows.Count > 0)
        {
            host = dataTable.Rows[0]["Id"].ToString();
        }
    }

    uri = new Uri(host + context.Request.Path.ToString().Replace("/-" + customer, "") + context.Request.QueryString);

    // Construye la URL de destino
    // uri = new Uri("http://127.0.0.1:8081" + context.Request.Path + context.Request.QueryString);    

    requestMessage.RequestUri = uri;

    // Copia los encabezados de la solicitud, excluyendo algunos que pueden causar problemas
    foreach (var header in context.Request.Headers)
    {
        if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
        {
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    // Copia el contenido de la solicitud si lo hay
    if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(header.Value);
            }
            else if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Content.Headers.ContentLength = long.Parse(header.Value);
            }
            else if (header.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Content.Headers.ContentEncoding.Add(header.Value);
            }
        }
    }

    // Envía la solicitud al servidor de destino
    var responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

    // Copia los encabezados de la respuesta, excluyendo algunos que pueden causar problemas
    foreach (var header in responseMessage.Headers)
    {
        if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) &&
            !header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
    }
    foreach (var header in responseMessage.Content.Headers)
    {
        if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
    }

    // Establece el código de estado de la respuesta
    context.Response.StatusCode = (int)responseMessage.StatusCode;

    // Importante: Limpia el encabezado Transfer-Encoding si está presente
    context.Response.Headers.Remove("transfer-encoding");

    // Copia el contenido de la respuesta al cuerpo de la respuesta
    await responseMessage.Content.CopyToAsync(context.Response.Body);
}));



string SQLMem( string sql)
{
    try
    {
        DataTable t = mem.SQLTable(sql);
        return db.GetJson(t);
    }
    catch (Exception e)
    {
        return $"Error: {e.Message}";
    }
}

string CreateHostId(Dictionary<string,string> d)
{
    try
    {

        HostData hostData = new HostData
        {
            Id = d["create_host"],
            Account = d["account"],
            Branch = d["branch"],
            Host = d["host"],
            Active = 1,
        };

        return db.UpsertInto("HostData", hostData);

    }
    catch (Exception e)  
    {
        return $"Error: {e.Message}";
    }
}


void PutHeader()
{
    Console.Clear();
    AngelDB.Monitor.ShowLine("===================================================================", ConsoleColor.DarkGreen);
    AngelDB.Monitor.ShowLine(" =>  Proxy software, powerful and simple at the same time", ConsoleColor.DarkGreen);
    AngelDB.Monitor.ShowLine(" =>  We explain it to you in 20 words or fewer:", ConsoleColor.DarkGreen);
    AngelDB.Monitor.ShowLine(" =>  AngelProxy", ConsoleColor.Green);
    AngelDB.Monitor.ShowLine("APP DIRECTORY: " + app_directory, ConsoleColor.Gray);
    AngelDB.Monitor.ShowLine("===================================================================", ConsoleColor.DarkGreen);
}


if (WindowsServiceHelpers.IsWindowsService())
{
    app.Run();
}
else
{
    app.RunAsync();

    DbLanguage language = new DbLanguage();
    language.SetCommands(AngelSQL.ProxyCommands.DbCommands());
    Dictionary<string, string> d = new Dictionary<string, string>();

    PutHeader();

    // All operations are done here
    string line;
    string prompt = "Proxy";
    string prompt_result = "";

    for (; ; )
    {

        line = AngelDB.Monitor.Prompt(prompt + " $> ");

        if (string.IsNullOrEmpty(line))
        {
            continue;
        }

        d = language.Interpreter(line);

        if (d != null) 
        {
            switch (d.First().Key)
            {
                case "create_host":
                    prompt_result = CreateHostId(d);
                    Console.WriteLine(prompt_result);
                    continue;

                case "mem":

                    prompt_result = SQLMem(d["mem"]);
                    Console.WriteLine(prompt_result);
                    continue;

                default:
                    AngelDB.Monitor.ShowError($"Error: Invalid command {language.errorString}");
                    continue;
            }
        }

        if (line.Trim().ToUpper() == "QUIT")
        {
            Environment.Exit(0);
            return;
        }

        if (line.Trim().ToUpper() == "CLEAR")
        {
            PutHeader();
            continue;
        }

        if (line.Trim().ToUpper() == "LISTEN ON")
        {
            try
            {
                foreach (string item in app.Urls)
                {
                    Console.WriteLine(item);
                }
            }
            catch (Exception e)
            {
                AngelDB.Monitor.ShowError($"Error: {e.ToString()}");
            }

            continue;

        }

    }
}






