using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenIt.Core;

internal static class CommentFormattingChecks
{
    internal static async Task Run(Action<bool,string> check)
    {
        using var owner = new Coordinator(showTray:false,registerHotkey:false);
        var image = BitmapSource.Create(32,32,96,96,PixelFormats.Bgr32,null,new byte[32*32*4],32*4); image.Freeze();
        void Commit(ScreenshotDraft draft)
        {
            var shot = owner.Session.Commit(draft); owner.Rasters.Add(shot.Id,new(image,image));
        }
        var a = owner.Session.CreateDraft(32,32); a.CreateMarker(new(10,10),"Тест"); Commit(a);
        var b = owner.Session.CreateDraft(32,32); b.AddPencil([new(1,1),new(20,20)]); Commit(b);
        var c = owner.Session.CreateDraft(32,32); var marker = c.CreateMarker(new(10,10),"Ещё комментарий\n第二行"); Commit(c);
        string expected = "Screenshot A" + Environment.NewLine + "A1 — Тест" + Environment.NewLine + Environment.NewLine
            + "Screenshot C" + Environment.NewLine + "C1 — Ещё комментарий" + Environment.NewLine + "     第二行";
        check(CommentFormatter.Format(owner.Session) == expected,"Session text contains A/C with Unicode multiline comments, omits unmarked B");
        check(b.Comments() == "" && CommentFormatter.Format("B",b.Items) == "","Single unmarked screenshot has no heading");
        var snapshot = owner.Session.Screenshots.ToArray();
        check(CommentFormatter.Format(snapshot) == expected,"Snapshot and Copy Comments share formatter");
        var plan = await owner.PreparePaste();
        check(plan.ImageCount == 3 && plan.Steps.Select(s => s.IsImage).SequenceEqual(new[] { true,true,true,false }),"Paste retains all three images before filtered text");
        check(plan.Steps.Where(s => s.IsImage).Select(s => Path.GetFileName(s.ImagePath)).SequenceEqual(new[] { "ScreenIt-A.png","ScreenIt-B.png","ScreenIt-C.png" }),"Image letters and order stay A/B/C");
        check(Encoding.Unicode.GetString(plan.Steps.Last().Payload.Single(p => p.Format == 13).Bytes) == expected + "\0","Prepared Unicode clipboard payload contains only A/C");
        check(owner.Session.Screenshots.SequenceEqual(snapshot) && owner.Session.NextLetter == "D" && marker.Label == "C1","Formatting/preparation preserve session, screenshot letters and marker labels");
        var empty = new Session();
        check(CommentFormatter.Format(empty) == "","Empty session has no comment text");
        owner.Session.Clear(); owner.Rasters.Clear();
        Commit(owner.Session.CreateDraft(32,32));
        var shapes = owner.Session.CreateDraft(32,32); shapes.AddArrow(new(1,1),new(20,20)); shapes.AddBox(new(1,1),new(20,20)); Commit(shapes);
        var deleted = owner.Session.CreateDraft(32,32); var removed = deleted.CreateMarker(new(10,10),"removed"); deleted.Delete(removed.Id); Commit(deleted);
        check(CommentFormatter.Format(owner.Session) == "","Empty, shape-only and deleted-marker screenshots produce no text");
        var textless = await owner.PreparePaste();
        check(textless.ImageCount == 3 && textless.Steps.Count == 3 && textless.Steps.All(s => s.IsImage),"Unmarked session pastes every image without a text step");
        var gaps = new Session(); gaps.Commit(gaps.CreateDraft(32,32));
        var middle = gaps.CreateDraft(32,32); middle.CreateMarker(new(10,10),"only B"); gaps.Commit(middle); gaps.Commit(gaps.CreateDraft(32,32));
        check(CommentFormatter.Format(gaps) == "Screenshot B" + Environment.NewLine + "B1 — only B","Leading/trailing unmarked screenshots add no separators or renumbering");
    }
}
