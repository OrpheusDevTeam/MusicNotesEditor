<!-- TOC start (generated with https://github.com/derlin/bitdowntoc) -->
### Table of contents
- [MusicNotessEditor.LocalServer](#musicnotesseditorlocalserver)
   * [Endpoints](#endpoints)
   * [`GET /cert`](#get-cert)
   * [`GET /ping`](#get-ping)
   * [`GET /pairinfo`](#get-pairinfo)
   * [`POST /request_access`](#post-request_access)
   * [`GET /pending`](#get-pending)
   * [`POST /upload`](#post-upload)
   * [`POST /disconnect`](#post-disconnect)

<!-- TOC end -->

<!-- TOC --><a name="local-server-api-reference"></a>

<br/>
<br/>


# MusicNotessEditor.LocalServer

This document describes the HTTP API exposed by the Eurydice Local Server  
implemented in `MusicNotesEditor.LocalServer`.

Base URL:
```
https://<local-ip>:5003
```

Authentication:
- Every request (except `/cert`, `/ping`, `/pairinfo`) must send:
  - `X-Token: <token>`
- Uploads must additionally provide:
  - `DeviceID: <id>`

The token and server URL are returned by `StartServerAsync` and `/pairinfo`.

---

## Endpoints

---

## `GET /cert`
Returns the server’s SSL certificate in PEM format.

**Response:**  
`text/x-pem-file`

---

## `GET /ping`
Health-check endpoint.

**Response:**  
`200 OK` with body: `"pong"`

---

## `GET /pairinfo`
Returns handshake info for QR pairing.

**Response JSON:**
```json
{
  "url": "https://<ip>:5003",
  "token": "<token>",
  "fp": "<sha256-fingerprint>"
}
```

---

## `POST /request_access`
Requests permission to connect.

**Headers:**  
`X-Token: <token>`

**Body JSON:**
```json
{
  "deviceName": "MyPhone"
}
```

**Response:**
```json
{
  "id": "<generated-id>"
}
```

The ID must be used in subsequent `/upload` calls.

---

## `GET /pending`
Returns list of devices awaiting approval.

**Response JSON:**
```json
[
  {
    "key": "<id>",
    "time": "<timestamp>",
    "deviceName": "PhoneName"
  }
]
```

## `POST /upload`
Uploads an image file via raw binary body.

**Headers:**
```
X-Token: <token>
DeviceID: <id>
Content-Type: image/jpeg
```

**Body:** raw JPEG bytes.

**Response JSON:**
```json
{
  "ok": true,
  "file": "<generated-file-name>",
  "path": "<full-local-path>"
}
```

## `POST /disconnect`
Removes all entries for a device.

**Headers:**
`X-Token: <token>`

**Body JSON:**
```json
{
  "deviceName": "MyPhone"
}
```

**Response JSON:**
```json
{
  "ok": true,
  "removed": <count>
}
```
