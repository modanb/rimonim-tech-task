$body = Get-Content -Raw .\test_reading.json
$req = [System.Net.WebRequest]::Create('http://127.0.0.1:5002/api/readings')
$req.Method = 'POST'
$req.ContentType = 'application/json'
$bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
$req.ContentLength = $bytes.Length
$stream = $req.GetRequestStream()
$stream.Write($bytes,0,$bytes.Length)
$stream.Close()
try {
    $res = $req.GetResponse()
    $httpRes = $res -as [System.Net.HttpWebResponse]
    $status = if ($null -ne $httpRes) { $httpRes.StatusCode.value__ } else { 'unknown' }
    $sr = New-Object System.IO.StreamReader($res.GetResponseStream())
    $bodyResponse = $sr.ReadToEnd()
    Write-Output "STATUS: $status"
    if ($bodyResponse) { Write-Output "BODY: $bodyResponse" }
} catch [System.Net.WebException] {
    if ($null -ne $_.Response) {
        $httpRes = $_.Response -as [System.Net.HttpWebResponse]
        $status = if ($null -ne $httpRes) { $httpRes.StatusCode.value__ } else { 'unknown' }
        $sr = New-Object System.IO.StreamReader($_.Response.GetResponseStream())
        Write-Output "ERROR STATUS: $status"
        Write-Output ('ERROR BODY: ' + $sr.ReadToEnd())
    } else {
        Write-Output ('ERROR: ' + $_.Exception.Message)
    }
}