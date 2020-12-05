# WinWebDetect

## Usage
*WinWebDetect [/i INTERVAL] URL [TRACKERS...] [/ URL [TRACKERS...] [/ URL [TRACKERS...] - ...]]*
    
Where URL is a URL to request and TRACKERS are strings to look for in the request

## Flags
    /               NONE            Seperator
    /a, /async      NONE            Query each URL asynchronously (at the same time)
    /b, /browser    STRING          Set which browser's cookies to use in http requests (""edge"", ""chrome"", or ""none"")
    /i, /interval   FLOAT           Amount of time between each check, in seconds
    
    /y              NONE            Automatically open url when change detected (popup box still appears)
    /s              NONE            Marks the desired state of the latest tracker as FALSE
    /S              NONE            Marks the desired state of the latest tracker as TRUE
    /n, /name       NONE            Sets name of the latest url
    /w, /warn       STRING          Marked tracker will warn if met desired state
    
    /d, /define     (NAME)=(ARGS)   Define a profile
    /p, /profile    STRING          Use a profile

## Notes
    any URL or TRACKER argument may instead be a file path
    Files are read the same way as arguments, with newlines also acting as seperators

    The program will attempt to read arguments from webdetect.txt if no URLs are provided
