using HygieneAudit.Domain.Entities;
using System.IO.Compression;
using System.Text;

namespace HygieneAudit.Application.Services;

public static class ExcelBuilder
{
    // photo dimensions
    const long ImgW  = 1_143_000L; // 120 px × 9525 EMU/px
    const long ImgH  =   857_250L; //  90 px × 9525 EMU/px
    const long PinOff =    76_200L; //   8 px cell-edge padding
    const double PhotoRowHt = 85.0; // row height (pts) for rows containing photos
    const int MaxPhotos     = 5;
    const int PhotoStartCol = 7;    // 0-based index = column H

    // style indices (see StyleSheet())
    // 0 default | 1 blue header | 2 indigo group header
    // 3 data cell | 4 alt data cell | 5 PASS | 6 FAIL

    static readonly string[] Cols = { "A","B","C","D","E","F","G","H","I","J","K","L" };

    private struct Img { public string File; public string Ext; public byte[] Data; }
    private struct Pin { public int Col; public int Row; public string Rid; public int Id; } // Row is 0-based

    // ── Public entry point ────────────────────────────────────────────────────────

    public static byte[] Build(IEnumerable<Audit> audits, string? uploadsFolder = null)
    {
        var list   = audits.OrderByDescending(a => a.Date).ToList();
        var imgs   = new List<Img>();
        var pins   = new List<Pin>();
        var merges = new List<string>();
        var detail = BuildDetailRows(list, imgs, pins, merges, uploadsFolder);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Txt(zip, "[Content_Types].xml",              ContentTypes(imgs));
            Txt(zip, "_rels/.rels",                      RootRels());
            Txt(zip, "xl/workbook.xml",                  Workbook());
            Txt(zip, "xl/_rels/workbook.xml.rels",       WorkbookRels());
            Txt(zip, "xl/styles.xml",                    StyleSheet());
            Txt(zip, "xl/worksheets/sheet1.xml",         SummarySheet(list));
            Txt(zip, "xl/worksheets/sheet2.xml",         DetailSheet(detail, merges, imgs.Count > 0));
            Txt(zip, "xl/worksheets/_rels/sheet2.xml.rels", Sheet2Rels(imgs.Count > 0));
            if (imgs.Count > 0)
            {
                Txt(zip, "xl/drawings/drawing1.xml",              DrawingXml(pins));
                Txt(zip, "xl/drawings/_rels/drawing1.xml.rels",   DrawingRels(imgs));
                foreach (var img in imgs) Raw(zip, "xl/media/" + img.File, img.Data);
            }
        }
        ms.Position = 0;
        return ms.ToArray();
    }

    // ── ZIP helpers ───────────────────────────────────────────────────────────────

    static void Txt(ZipArchive z, string path, string content)
    {
        var e = z.CreateEntry(path, CompressionLevel.Fastest);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    static void Raw(ZipArchive z, string path, byte[] data)
    {
        var e = z.CreateEntry(path, CompressionLevel.Fastest);
        using var s = e.Open();
        s.Write(data, 0, data.Length);
    }

    // ── XML cell helpers ──────────────────────────────────────────────────────────

    static string Esc(string? s) =>
        string.IsNullOrEmpty(s) ? "" :
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // Text cell (inlineStr)
    static string Tc(int col, int row, int s, string? text)
    {
        var r = Cols[col] + row;
        return string.IsNullOrEmpty(text)
            ? $"<c r=\"{r}\" s=\"{s}\"/>"
            : $"<c r=\"{r}\" s=\"{s}\" t=\"inlineStr\"><is><t>{Esc(text)}</t></is></c>";
    }

    // Numeric cell
    static string Nc(int col, int row, int s, object n)
        => $"<c r=\"{Cols[col]}{row}\" s=\"{s}\"><v>{n}</v></c>";

    // ── Static XML parts ──────────────────────────────────────────────────────────

    static string ContentTypes(List<Img> imgs)
    {
        var sb = new StringBuilder(1024);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        sb.Append("<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        if (imgs.Count > 0)
        {
            sb.Append("<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>");
            foreach (var ext in imgs.Select(i => i.Ext).Distinct())
                sb.Append($"<Default Extension=\"{ext}\" ContentType=\"{(ext == "png" ? "image/png" : "image/jpeg")}\"/>");
        }
        sb.Append("</Types>");
        return sb.ToString();
    }

    static string RootRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    static string Workbook() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets>" +
        "<sheet name=\"Ringkasan\" sheetId=\"1\" r:id=\"rId1\"/>" +
        "<sheet name=\"Detail Checklist\" sheetId=\"2\" r:id=\"rId2\"/>" +
        "</sheets>" +
        "</workbook>";

    static string WorkbookRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
        "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    static string Sheet2Rels(bool hasImages) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        (hasImages ? "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>" : "") +
        "</Relationships>";

    static string StyleSheet() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<fonts count=\"5\">" +
        "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +                                         // 0 normal
        "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +                                     // 1 bold
        "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +            // 2 bold white
        "<font><sz val=\"11\"/><color rgb=\"FF22C55E\"/><name val=\"Calibri\"/></font>" +                // 3 green
        "<font><sz val=\"11\"/><color rgb=\"FFEF4444\"/><name val=\"Calibri\"/></font>" +                // 4 red
        "</fonts>" +
        "<fills count=\"5\">" +
        "<fill><patternFill patternType=\"none\"/></fill>" +                                             // 0 required
        "<fill><patternFill patternType=\"gray125\"/></fill>" +                                          // 1 required
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF4472C4\"/></patternFill></fill>" +    // 2 blue header
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF696CFF\"/></patternFill></fill>" +    // 3 indigo group
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8F0FE\"/></patternFill></fill>" +    // 4 light alt
        "</fills>" +
        "<borders count=\"2\">" +
        "<border><left/><right/><top/><bottom/><diagonal/></border>" +
        "<border>" +
        "<left style=\"thin\"><color rgb=\"FFD0D0DC\"/></left>" +
        "<right style=\"thin\"><color rgb=\"FFD0D0DC\"/></right>" +
        "<top style=\"thin\"><color rgb=\"FFD0D0DC\"/></top>" +
        "<bottom style=\"thin\"><color rgb=\"FFD0D0DC\"/></bottom>" +
        "<diagonal/>" +
        "</border>" +
        "</borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"7\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +                    // 0 default
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf>" +  // 1 blue hdr
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf>" +  // 2 indigo hdr
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +                   // 3 data
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf>" +   // 4 alt data
        "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" + // 5 PASS
        "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" + // 6 FAIL
        "</cellXfs>" +
        "</styleSheet>";

    // ── Summary sheet (sheet1) ────────────────────────────────────────────────────

    static string SummarySheet(List<Audit> list)
    {
        var sb = new StringBuilder(4096);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<cols>");
        sb.Append("<col min=\"1\"  max=\"1\"  width=\"5\"  customWidth=\"1\"/>");
        sb.Append("<col min=\"2\"  max=\"2\"  width=\"22\" customWidth=\"1\"/>");
        sb.Append("<col min=\"3\"  max=\"3\"  width=\"12\" customWidth=\"1\"/>");
        sb.Append("<col min=\"4\"  max=\"4\"  width=\"14\" customWidth=\"1\"/>");
        sb.Append("<col min=\"5\"  max=\"5\"  width=\"18\" customWidth=\"1\"/>");
        sb.Append("<col min=\"6\"  max=\"6\"  width=\"12\" customWidth=\"1\"/>");
        sb.Append("<col min=\"7\"  max=\"7\"  width=\"12\" customWidth=\"1\"/>");
        sb.Append("<col min=\"8\"  max=\"8\"  width=\"8\"  customWidth=\"1\"/>");
        sb.Append("<col min=\"9\"  max=\"9\"  width=\"8\"  customWidth=\"1\"/>");
        sb.Append("<col min=\"10\" max=\"10\" width=\"8\"  customWidth=\"1\"/>");
        sb.Append("<col min=\"11\" max=\"11\" width=\"35\" customWidth=\"1\"/>");
        sb.Append("</cols>");
        sb.Append("<sheetData>");

        int r = 1;

        // Title (merged A1:K1)
        sb.Append($"<row r=\"{r}\" ht=\"22\" customHeight=\"1\">");
        sb.Append(Tc(0, r, 2, $"Laporan Audit Hygiene - Dicetak {DateTime.Now:dd/MM/yyyy HH:mm}"));
        for (int c = 1; c < 11; c++) sb.Append($"<c r=\"{Cols[c]}{r}\" s=\"2\"/>");
        sb.Append("</row>");
        r++;

        // Column headers
        sb.Append($"<row r=\"{r}\" ht=\"20\" customHeight=\"1\">");
        var hdr = new[] { "No","Tenant","Tipe","Tanggal","PIC","Status","Pass Rate","Total","Pass","Fail","Catatan Fail" };
        for (int c = 0; c < hdr.Length; c++) sb.Append(Tc(c, r, 1, hdr[c]));
        sb.Append("</row>");
        r++;

        bool alt = false;
        int no = 1;
        foreach (var a in list)
        {
            var total = a.Items.Count;
            var pass  = a.Items.Count(i => i.Status == AuditItemStatus.Pass);
            var fail  = a.Items.Count(i => i.Status == AuditItemStatus.Fail);
            var rate  = total > 0 ? Math.Round((double)pass / total * 100, 1) : 0.0;
            var notes = string.Join("; ", a.Items
                .Where(i => i.Status == AuditItemStatus.Fail && !string.IsNullOrWhiteSpace(i.Note))
                .Select(i => i.Note!));
            int s = alt ? 4 : 3;

            sb.Append($"<row r=\"{r}\">");
            sb.Append(Nc(0,  r, s, no));
            sb.Append(Tc(1,  r, s, a.Tenant?.Name));
            sb.Append(Tc(2,  r, s, a.IsGas ? "Dengan Gas" : "Tanpa Gas"));
            sb.Append(Tc(3,  r, s, a.Date.ToString("dd/MM/yyyy")));
            sb.Append(Tc(4,  r, s, a.Pic?.Name));
            sb.Append(Tc(5,  r, s, a.Status.ToString()));
            sb.Append(Tc(6,  r, s, rate.ToString("0.0") + "%"));
            sb.Append(Nc(7,  r, s, total));
            sb.Append(Nc(8,  r, s, pass));
            sb.Append(Nc(9,  r, s, fail));
            sb.Append(Tc(10, r, s, notes));
            sb.Append("</row>");
            no++; r++; alt = !alt;
        }

        sb.Append("</sheetData>");
        sb.Append("<mergeCells count=\"1\"><mergeCell ref=\"A1:K1\"/></mergeCells>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    // ── Detail sheet (sheet2) ─────────────────────────────────────────────────────

    static string BuildDetailRows(List<Audit> list, List<Img> imgs, List<Pin> pins, List<string> merges, string? uploadsFolder = null)
    {
        var sb     = new StringBuilder(16384);
        int row    = 1;
        int imgId  = 1;
        int itemNo = 1;

        // Title row
        sb.Append($"<row r=\"{row}\" ht=\"22\" customHeight=\"1\">");
        sb.Append(Tc(0, row, 2, $"Detail Checklist - Dicetak {DateTime.Now:dd/MM/yyyy HH:mm}"));
        for (int c = 1; c < 12; c++) sb.Append($"<c r=\"{Cols[c]}{row}\" s=\"2\"/>");
        sb.Append("</row>");
        merges.Add($"A{row}:L{row}");
        row++;

        foreach (var a in list)
        {
            // Audit group header
            var groupText = $"{a.Tenant?.Name ?? ""}  |  {a.Date:dd/MM/yyyy}  |  PIC: {a.Pic?.Name ?? ""}  |  {a.Status}";
            sb.Append($"<row r=\"{row}\" ht=\"20\" customHeight=\"1\">");
            sb.Append(Tc(0, row, 2, groupText));
            for (int c = 1; c < 12; c++) sb.Append($"<c r=\"{Cols[c]}{row}\" s=\"2\"/>");
            sb.Append("</row>");
            merges.Add($"A{row}:L{row}");
            row++;

            // Column header row
            sb.Append($"<row r=\"{row}\" ht=\"18\" customHeight=\"1\">");
            var ch = new[] { "No","Tanggal","Tenant","Kategori","Item Checklist","Status","Catatan","Foto 1","Foto 2","Foto 3","Foto 4","Foto 5" };
            for (int c = 0; c < ch.Length; c++) sb.Append(Tc(c, row, 1, ch[c]));
            sb.Append("</row>");
            row++;

            bool alt = false;
            foreach (var item in a.Items.OrderBy(i => i.Category).ThenBy(i => i.Id))
            {
                var photos  = item.Photos.Take(MaxPhotos).ToList();
                bool hasImg = photos.Count > 0;
                int  s      = alt ? 4 : 3;
                int  sSt    = item.Status == AuditItemStatus.Pass ? 5
                            : item.Status == AuditItemStatus.Fail ? 6 : 3;
                string htAttr = hasImg ? $" ht=\"{PhotoRowHt}\" customHeight=\"1\"" : "";

                sb.Append($"<row r=\"{row}\"{htAttr}>");
                sb.Append(Nc(0, row, s, itemNo));
                sb.Append(Tc(1, row, s, a.Date.ToString("dd/MM/yyyy")));
                sb.Append(Tc(2, row, s, a.Tenant?.Name));
                sb.Append(Tc(3, row, s, item.Category));
                sb.Append(Tc(4, row, s, item.Name));
                sb.Append(Tc(5, row, sSt, item.Status == AuditItemStatus.Pass ? "PASS"
                                          : item.Status == AuditItemStatus.Fail ? "FAIL" : "-"));
                sb.Append(Tc(6, row, s, item.Note));
                for (int pc = 0; pc < MaxPhotos; pc++)
                    sb.Append($"<c r=\"{Cols[PhotoStartCol + pc]}{row}\" s=\"{s}\"/>");
                sb.Append("</row>");

                // Collect image anchors
                for (int pi = 0; pi < photos.Count; pi++)
                {
                    var (ext, data) = ResolvePhoto(photos[pi].PhotoUrl, uploadsFolder);
                    if (data.Length == 0) continue;
                    imgs.Add(new Img { File = $"image{imgId}.{ext}", Ext = ext, Data = data });
                    pins.Add(new Pin { Col = PhotoStartCol + pi, Row = row - 1, Rid = $"rId{imgId}", Id = imgId });
                    imgId++;
                }

                itemNo++; row++; alt = !alt;
            }

            // Blank spacer row between audit sections
            sb.Append($"<row r=\"{row}\"/>");
            row++;
        }

        return sb.ToString();
    }

    static string DetailSheet(string rowsXml, List<string> merges, bool hasImages)
    {
        var sb = new StringBuilder(4096 + rowsXml.Length);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                  "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        sb.Append("<cols>");
        sb.Append("<col min=\"1\"  max=\"1\"  width=\"5\"  customWidth=\"1\"/>");
        sb.Append("<col min=\"2\"  max=\"2\"  width=\"14\" customWidth=\"1\"/>");
        sb.Append("<col min=\"3\"  max=\"3\"  width=\"22\" customWidth=\"1\"/>");
        sb.Append("<col min=\"4\"  max=\"4\"  width=\"18\" customWidth=\"1\"/>");
        sb.Append("<col min=\"5\"  max=\"5\"  width=\"30\" customWidth=\"1\"/>");
        sb.Append("<col min=\"6\"  max=\"6\"  width=\"10\" customWidth=\"1\"/>");
        sb.Append("<col min=\"7\"  max=\"7\"  width=\"25\" customWidth=\"1\"/>");
        sb.Append("<col min=\"8\"  max=\"12\" width=\"18\" customWidth=\"1\"/>");
        sb.Append("</cols>");
        sb.Append("<sheetData>").Append(rowsXml).Append("</sheetData>");
        if (merges.Count > 0)
        {
            sb.Append($"<mergeCells count=\"{merges.Count}\">");
            foreach (var m in merges) sb.Append($"<mergeCell ref=\"{m}\"/>");
            sb.Append("</mergeCells>");
        }
        if (hasImages) sb.Append("<drawing r:id=\"rId1\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    // ── Drawing XML ───────────────────────────────────────────────────────────────

    static string DrawingXml(List<Pin> pins)
    {
        var sb = new StringBuilder(512 * pins.Count + 256);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" " +
                  "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
                  "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        foreach (var p in pins)
        {
            sb.Append("<xdr:oneCellAnchor>");
            sb.Append($"<xdr:from><xdr:col>{p.Col}</xdr:col><xdr:colOff>{PinOff}</xdr:colOff>" +
                      $"<xdr:row>{p.Row}</xdr:row><xdr:rowOff>{PinOff}</xdr:rowOff></xdr:from>");
            sb.Append($"<xdr:ext cx=\"{ImgW}\" cy=\"{ImgH}\"/>");
            sb.Append("<xdr:pic>");
            sb.Append($"<xdr:nvPicPr><xdr:cNvPr id=\"{p.Id}\" name=\"Foto{p.Id}\"/><xdr:cNvPicPr/></xdr:nvPicPr>");
            sb.Append($"<xdr:blipFill><a:blip r:embed=\"{p.Rid}\"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>");
            sb.Append($"<xdr:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{ImgW}\" cy=\"{ImgH}\"/></a:xfrm>" +
                      $"<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></xdr:spPr>");
            sb.Append("</xdr:pic><xdr:clientData/></xdr:oneCellAnchor>");
        }
        sb.Append("</xdr:wsDr>");
        return sb.ToString();
    }

    static string DrawingRels(List<Img> imgs)
    {
        var sb = new StringBuilder(256 + imgs.Count * 160);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (int i = 0; i < imgs.Count; i++)
            sb.Append($"<Relationship Id=\"rId{i + 1}\" " +
                      $"Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" " +
                      $"Target=\"../media/{imgs[i].File}\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // ── Photo resolver ────────────────────────────────────────────────────────────

    // Handles both legacy base64 data URLs and current filename storage.
    static (string ext, byte[] data) ResolvePhoto(string? url, string? uploadsFolder)
    {
        if (string.IsNullOrEmpty(url)) return ("jpg", Array.Empty<byte>());

        // Legacy: base64 data URL
        if (url.StartsWith("data:"))
        {
            try
            {
                int semi  = url.IndexOf(';');
                if (semi < 0) return ("jpg", Array.Empty<byte>());
                string mime = url.Substring(5, semi - 5);
                int comma = url.IndexOf(',', semi);
                if (comma < 0) return ("jpg", Array.Empty<byte>());
                byte[] bytes = Convert.FromBase64String(url.Substring(comma + 1));
                string ext   = mime.Contains("png") ? "png" : mime.Contains("gif") ? "gif" : "jpg";
                return (ext, bytes);
            }
            catch { return ("jpg", Array.Empty<byte>()); }
        }

        // Current: filename stored on disk
        if (!string.IsNullOrEmpty(uploadsFolder))
        {
            try
            {
                var safeName = System.IO.Path.GetFileName(url); // guard path traversal
                var path     = System.IO.Path.Combine(uploadsFolder, safeName);
                if (!System.IO.File.Exists(path)) return ("jpg", Array.Empty<byte>());
                var data = System.IO.File.ReadAllBytes(path);
                var ext  = safeName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "png"
                         : safeName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "gif" : "jpg";
                return (ext, data);
            }
            catch { return ("jpg", Array.Empty<byte>()); }
        }

        return ("jpg", Array.Empty<byte>());
    }
}
