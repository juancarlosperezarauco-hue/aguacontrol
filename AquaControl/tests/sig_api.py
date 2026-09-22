"""Read-only SIG smoke tests. Authentication creates only session/login metadata.
Use --password-file with the local access file; no credentials are printed.
"""
import argparse
import http.cookiejar
import json
import time
import urllib.error
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('url')
parser.add_argument('--password-file', required=True)
parser.add_argument('--report', required=True)
args = parser.parse_args()
access = Path(args.password_file).read_text(encoding='utf-8-sig').splitlines()
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
checks = []

def request(path, data=None, token=None, expected=200):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['X-CSRF-TOKEN'] = token
    req = urllib.request.Request(args.url + '/api' + path, data=json.dumps(data).encode() if data else None, headers=headers)
    start = time.monotonic()
    try:
        with opener.open(req, timeout=120) as response:
            status, body = response.status, response.read()
    except urllib.error.HTTPError as error:
        status, body = error.code, error.read()
    assert status == expected, (path, status, body[:300])
    checks.append({'path': path, 'status': status, 'seconds': round(time.monotonic() - start, 3)})
    return json.loads(body) if body else None

request('/geo-summary', expected=401)
csrf = request('/auth/csrf')['token']
request('/auth/login', {'login': access[0].split(': ', 1)[1], 'password': access[1].split(': ', 1)[1]}, csrf)
layers = request('/geo-summary')
assert len(layers) == 4
for layer in layers:
    assert layer['spatialIndex'], layer
    # Detailed validity results belong to --validate-sig, not to interactive map queries.
    assert layer['invalid'] is layer['wrongSrid'] is layer['missingGeometry'] is None, layer
    west, south, east, north = layer['bounds']
    collection = request(f"/geo/{layer['layer']}?west={west-.00001}&south={south-.00001}&east={east+.00001}&north={north+.00001}")
    assert collection['type'] == 'FeatureCollection'
    assert len(collection['features']) == min(1000, layer['count'])
    assert collection['truncated'] == (layer['count'] > 1000)
    assert len({feature['id'] for feature in collection['features']}) == len(collection['features'])
    for feature in collection['features']:
        assert feature['geometry']['type'] in {'Point', 'MultiPolygon', 'Polygon', 'MultiLineString', 'LineString'}
    empty = request(f"/geo/{layer['layer']}?west=0&south=0&east=1&north=1")
    assert not empty['features'] and not empty['truncated']
request('/geo/codes?west=1&south=0&east=0&north=1', expected=400)
request('/geo/codes?west=-181&south=0&east=0&north=1', expected=400)
request('/geo/unknown?west=0&south=0&east=1&north=1', expected=400)
Path(args.report).write_text(json.dumps({'result': 'PASS', 'checks': checks}, indent=2), encoding='utf-8')
print(f'PASS: {len(checks)} peticiones verificadas. Informe: {args.report}')
