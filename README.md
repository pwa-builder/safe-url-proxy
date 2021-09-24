# safe-url-proxy
Azure function app that fetches a URL and returns its contents. This is used by PWABuilder to get around cross-origin request limitations.

## Usage

- To get a resource, like an image, call `/api/getSafeUrl?url=https://somesite/logo.png`

- To check for the existence of a resource, call `/api/getSafeUrl?checkExistsOnly=true&url=https://somesite/logo.png`

