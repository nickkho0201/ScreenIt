using System.Diagnostics;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

internal sealed record UpdateRelease(Version Version,Uri Page,Uri Installer,Uri Checksums);
internal sealed record DownloadProgress(long BytesDownloaded,long? TotalBytes);
internal interface IUpdateSource
{
    Task<UpdateRelease?> Check(CancellationToken token);
    Task Download(Uri uri,string path,long maxBytes,CancellationToken token,IProgress<DownloadProgress>? progress=null);
}
internal sealed class GithubUpdateSource : IUpdateSource
{
    internal const string Repository="https://github.com/nickkho0201/ScreenIt";
    internal static Version CurrentVersion { get; } = AppVersion();
    private static Version AppVersion() { var version=typeof(Coordinator).Assembly.GetName().Version!;return new(version.Major,version.Minor,version.Build); }
    internal static bool Allowed(Uri uri,bool redirect=false) => uri.Scheme=="https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && (uri.Host=="github.com" && uri.AbsolutePath.StartsWith("/nickkho0201/ScreenIt/releases/",StringComparison.Ordinal) || redirect && uri.Host=="release-assets.githubusercontent.com");
    internal static Version? StableVersion(string? tag) => tag!=null && Regex.IsMatch(tag,@"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$",RegexOptions.CultureInvariant) && Version.TryParse(tag.TrimStart('v'),out var v) ? v : null;
    internal static UpdateRelease? Parse(string json)
    {
        using var doc=JsonDocument.Parse(json);var r=doc.RootElement;
        if(r.GetProperty("draft").GetBoolean() || r.GetProperty("prerelease").GetBoolean()) return null;
        var version=StableVersion(r.GetProperty("tag_name").GetString()) ?? throw new InvalidDataException("Invalid release version.");
        if(version<=CurrentVersion) return null;
        string name=$"ScreenIt-Setup-{version}.exe";
        Uri Asset(string asset)
        {
            var values=r.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("name").GetString()==asset).ToArray();
            if(values.Length!=1) throw new InvalidDataException("Missing or ambiguous asset.");
            var uri=new Uri(values[0].GetProperty("browser_download_url").GetString()!);
            if(!Allowed(uri) || uri.AbsolutePath!=$"/nickkho0201/ScreenIt/releases/download/v{version}/{asset}") throw new InvalidDataException("Unexpected release asset URL.");return uri;
        }
        var page=new Uri(r.GetProperty("html_url").GetString()!);
        if(!Allowed(page) || page.AbsolutePath!=$"/nickkho0201/ScreenIt/releases/tag/v{version}") throw new InvalidDataException("Unexpected release page.");
        return new(version,page,Asset(name),Asset("SHA256SUMS.txt"));
    }
    private static HttpClient Client()
    {
        var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenIt/0.1.2");return client;
    }
    public async Task<UpdateRelease?> Check(CancellationToken token)
    {
        using var client=Client();client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2022-11-28");
        using var response=await client.GetAsync("https://api.github.com/repos/nickkho0201/ScreenIt/releases/latest",HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        using var memory=new MemoryStream();await LimitedCopy(await response.Content.ReadAsStreamAsync(token),memory,1024*1024,token);return Parse(System.Text.Encoding.UTF8.GetString(memory.ToArray()));
    }
    public async Task Download(Uri uri,string path,long maxBytes,CancellationToken token,IProgress<DownloadProgress>? progress=null)
    {
        if(!Allowed(uri)) throw new InvalidDataException("Unexpected download URL.");
        using var client=Client();
        for(int redirects=0;redirects<=4;redirects++)
        {
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode is >=300 and <400)
            { var location=response.Headers.Location ?? throw new InvalidDataException("Missing redirect.");uri=location.IsAbsoluteUri ? location : new Uri(uri,location);if(!Allowed(uri,true)) throw new InvalidDataException("Unexpected redirect.");continue; }
            response.EnsureSuccessStatusCode();
            long? length=response.Content.Headers.ContentLength;
            if(length>maxBytes) throw new InvalidDataException("Download too large.");
            using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous);
            await LimitedCopy(await response.Content.ReadAsStreamAsync(token),output,maxBytes,token,progress,length);return;
        }
        throw new InvalidDataException("Too many redirects.");
    }
    internal static async Task LimitedCopy(Stream input,Stream output,long limit,CancellationToken token,IProgress<DownloadProgress>? progress=null,long? expectedLength=null)
    {
        using(input)
        {
            byte[] buffer=new byte[81920];long total=0;int read;var timer=Stopwatch.StartNew();
            progress?.Report(new(0,expectedLength));
            while((read=await input.ReadAsync(buffer,token))>0)
            {
                total+=read;if(total>limit || expectedLength.HasValue && total>expectedLength.Value) throw new InvalidDataException("Download too large.");
                await output.WriteAsync(buffer.AsMemory(0,read),token);
                if(timer.ElapsedMilliseconds>=100) { progress?.Report(new(total,expectedLength));timer.Restart(); }
            }
            if(expectedLength.HasValue && total!=expectedLength.Value) throw new IOException("Incomplete download.");
            progress?.Report(new(total,expectedLength));
        }
    }
}
internal sealed class UpdateTransfer(IUpdateSource source)
{
    internal static string Root => Path.Combine(Path.GetTempPath(),"ScreenIt","Updates");
    internal static async Task Verify(string installer,string sums,CancellationToken token=default)
    {
        var lines=File.ReadAllLines(sums).Select(line=>Regex.Match(line,@"^(\S+)\s+\*?([^/\\]+)$")).Where(m=>m.Success && m.Groups[2].Value==Path.GetFileName(installer)).ToArray();
        if(lines.Length!=1 || !Regex.IsMatch(lines[0].Groups[1].Value,@"^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Missing or ambiguous checksum.");
        using var stream=File.OpenRead(installer);var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
        if(!hash.Equals(lines[0].Groups[1].Value,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Checksum mismatch.");
    }
    internal async Task<string> Prepare(UpdateRelease release,CancellationToken token,IProgress<DownloadProgress>? progress=null,Action? verifying=null)
    {
        Directory.CreateDirectory(Root);
        for(var dir=new DirectoryInfo(Root);dir!=null;dir=dir.Parent) if((dir.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Reparse update storage.");
        string folder=Path.Combine(Root,Guid.NewGuid().ToString("N"));
        var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        FileSystemAclExtensions.CreateDirectory(security,folder);
        string installer=Path.Combine(folder,$"ScreenIt-Setup-{release.Version}.exe"),sums=Path.Combine(folder,"SHA256SUMS.txt");
        File.WriteAllText(Path.Combine(folder,".screenit-update"),"ScreenIt update v1");
        try { await source.Download(release.Checksums,sums,65536,token);await source.Download(release.Installer,installer,256L*1024*1024,token,progress);verifying?.Invoke();await Verify(installer,sums,token);return installer; }
        catch { foreach(var file in new[]{installer,sums,Path.Combine(folder,".screenit-update")}) if(File.Exists(file)) File.Delete(file);Directory.Delete(folder);throw; }
    }
    internal static bool Installed()
    {
        using var hive=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64);
        using var key=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{75AF53B9-2BC6-4AC3-A7D4-859884B5EAF0}_is1");
        if(key?.GetValue("InstallLocation") is not string path || key.GetValue("DisplayName") is not string name || !name.StartsWith("ScreenIt",StringComparison.Ordinal)) return false;
        return Path.GetFullPath(path).TrimEnd('\\').Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(path,"unins000.exe"));
    }
    internal static void OpenPage(Uri uri)
    {
        if(uri.AbsoluteUri!=GithubUpdateSource.Repository && !GithubUpdateSource.Allowed(uri)) throw new InvalidOperationException("Unexpected page.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute=true });
    }
    internal static bool LaunchVerified(string path,Func<bool> confirm,Action<string> launch,Action close)
    {
        if(!confirm()) return false;launch(path);close();return true;
    }
}
