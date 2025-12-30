// HTTP POST with JSON
// Posts JSON to a URL. First line = URL, rest = JSON body

var lines = NotePlainText.Split('\n', 2);
if (lines.Length < 2)
{
    return "Format: First line = URL, remaining lines = JSON body";
}

var url = lines[0].Trim();
var json = lines[1].Trim();

var http = new HttpClient();
var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
var response = await http.PostAsync(url, content);
var result = await response.Content.ReadAsStringAsync();

Print($"Status: {(int)response.StatusCode} {response.StatusCode}");

return result;
