// HTTP GET Request
// Makes a GET request to a URL (paste URL in note first)

var url = NotePlainText.Trim();
if (!url.StartsWith("http"))
{
    return "Please paste a URL in the note first";
}

var http = new HttpClient();
var response = await http.GetAsync(url);
var content = await response.Content.ReadAsStringAsync();

Print($"Status: {(int)response.StatusCode} {response.StatusCode}");
Print($"Content-Type: {response.Content.Headers.ContentType}");

return content;
