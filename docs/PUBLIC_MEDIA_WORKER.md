# Public media companion worker

Create an owner-authored issue titled `public-media-worker: <label>` with a JSON body such as:

```json
{"task":"probe","url":"https://example.com/public-media.mp3","language":"en"}
```

Allowed tasks: `probe`, `audio-normalize`, `transcribe`.

Only public HTTPS media belongs here. Private recordings, Telegram media, credentials, customer data, and private Drive assets must stay out of the public worker.
