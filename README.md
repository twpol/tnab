# TNAB (The Not As Bad web browser)

Command-line, graphical tools and libraries for loading, rendering, browsing websites.

## CLI

```
TNAB (The Not As Bad web browser) CLI

Usage:
  TNAB.Cli [options] [action <URL> [...]] [...]

Options:
  /device-pixel-ratio <RATIO>  Set the device pixel ratio [default: 1.0]
  /screenshot <PATH>    Save a screenshot to the specified path
  /verbose              Enable verbose logging
  /verbose-cpu          Enable verbose logging of CPU usage
  /viewport <WxH>       Set the viewport size [default: 800x600]

Actions:
  /benchmark            Benchmark the HTML/CSS parser with the specified URLs
  /crash-test           Crash test the HTML/CSS parser with the specified URLs
  /load-document        Load navigable document from the specified URLs
  /print-boxes          Print the box tree from the specified URLs
  /print-dom            Print the HTML/CSS tree from the specified URLs
  /print-nodes          Print the HTML/CSS nodes from the specified URLs
  /print-tokens         Print the HTML/CSS tokens from the specified URLs

Aliases for Web Platform Tests:
  /crashtest            --> /crash-test
  /reftest              --> /load-document

Arguments:
  <URL>                 URL to load
```
