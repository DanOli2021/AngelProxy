// GLOBALS
// These lines of code go in each script
#load "Globals.csx"
// END GLOBALS

using System;
using System.Collections.Generic;

string result = db.Prompt("DB USER db PASSWORD db");

if(result.StartsWith("Error:"))
{
    result = db.Prompt("DB USER angel_proxy PASSWORD changeme12345");
}
else
{
    db.Prompt("CHANGE MASTER TO USER angel_proxy PASSWORD changeme12345", true);
    db.Prompt("DB USER angel_proxy PASSWORD changeme12345", true);
}

db.Prompt("CREATE ACCOUNT angelsql_sass SUPERUSER daniel PASSWORD changeme12345");
db.Prompt("USE ACCOUNT angelsql_sass");
db.Prompt("CREATE DATABASE angel_proxy");
db.Prompt("USE DATABASE angel_proxy");

List<Certificates> certificates = new List<Certificates>();

certificates.Add(new Certificates
{
    Domain = GetVariable("ANGELPROXY_DOMAIN1", ""),
    Certificate = GetVariable("ANGELPROXY_CERTIFICATE1", ""),
    Password = GetVariable("ANGELPROXY_CERTIFICATE1_PASSWORD", "")
});

certificates.Add(new Certificates
{
    Domain = GetVariable("ANGELPROXY_DOMAIN2", ""),
    Certificate = GetVariable("ANGELPROXY_CERTIFICATE2", ""),
    Password = GetVariable("ANGELPROXY_CERTIFICATE2_PASSWORD", "")
});

return db.GetJson(certificates);

string GetVariable(string name, string default_value)
{
    if (Environment.GetEnvironmentVariable(name) == null || Environment.GetEnvironmentVariable(name) == "") return default_value;
    Console.WriteLine($"Variable {name} found");
    return Environment.GetEnvironmentVariable(name);
}

public class Certificates
{
    public string Domain { get; set; } = "";
    public string Certificate { get; set; } = "";
    public string Password { get; set; } = "";
}


