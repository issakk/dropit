namespace DropLite.Models;

internal sealed record PresetCategory(string Name, string Pattern, string Target);

/// <summary>内置的常用文件分类掩码，可在设置里一键生成规则。</summary>
internal static class PresetCategories
{
    public static readonly PresetCategory[] All =
    {
        new("图片", "*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.tif;*.tiff;*.svg;*.heic;*.ico;*.raw;*.cr2;*.nef",
            "%USERPROFILE%\\Pictures\\图片"),
        new("视频", "*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.rmvb",
            "%USERPROFILE%\\Videos\\视频"),
        new("音频", "*.mp3;*.flac;*.wav;*.aac;*.ogg;*.m4a;*.wma;*.ape;*.opus;*.mid",
            "%USERPROFILE%\\Music\\音乐"),
        new("文档", "*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.rtf;*.csv;*.wps;*.et;*.dps",
            "%USERPROFILE%\\Documents\\文档"),
        new("压缩包", "*.zip;*.rar;*.7z;*.tar;*.gz;*.bz2;*.xz;*.cab",
            "%USERPROFILE%\\Documents\\压缩包"),
        new("光盘镜像", "*.iso;*.img;*.vhd;*.vhdx;*.mds;*.nrg",
            "%USERPROFILE%\\Documents\\光盘镜像"),
        new("电子书", "*.epub;*.mobi;*.azw3;*.azw;*.fb2;*.djvu;*.caj",
            "%USERPROFILE%\\Documents\\电子书"),
        new("安装包", "*.exe;*.msi;*.msix;*.appx;*.apk",
            "%USERPROFILE%\\Downloads\\安装包"),
        new("字体", "*.ttf;*.otf;*.fon;*.woff;*.woff2",
            "%USERPROFILE%\\Documents\\字体"),
        new("代码与脚本", "*.cs;*.py;*.js;*.ts;*.jsx;*.tsx;*.vue;*.java;*.c;*.cpp;*.h;*.go;*.rs;*.php;*.html;*.css;*.json;*.xml;*.yml;*.yaml;*.sql;*.sh;*.ps1;*.bat;*.cmd",
            "%USERPROFILE%\\Documents\\代码"),
        new("设计源文件", "*.psd;*.ai;*.cdr;*.xd;*.fig;*.sketch;*.indd;*.dwg;*.dxf",
            "%USERPROFILE%\\Documents\\设计文件"),
        new("种子文件", "*.torrent",
            "%USERPROFILE%\\Downloads\\种子"),
    };
}
