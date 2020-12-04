# WinWebDetect

## Usage
*WinWebDetect [/i INTERVAL] URL [TRACKERS...] [/ URL [TRACKERS...] [/ URL [TRACKERS...] - ...]]*
    
Where URL is a URL to request and TRACKERS are strings to look for in the request

## Flags
    /                   Seperator
    /a, /async          Query each URL asynchronously (at the same time)
    /i, /interval       Amount of time between each check
    /y                  Automatically open url when change detected (popup box still appears)
    /s                  Marks the desired state of the current tracker as FALSE
    /S                  Marks the desired state of the current tracker as TRUE
    /n, /name           Name of the current url

## Notes
    any URL or TRACKER argument may instead be a file path
    Files are read the same way as arguments, with newlines also acting as seperators

    The program will attempt to read arguments from webdetect.txt if no URLs are provided