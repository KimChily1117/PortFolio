import test from 'node:test';
import assert from 'node:assert/strict';
import { runInNewContext } from 'node:vm';
import { createRequire } from 'node:module';
import { NATIVE_WORLD_BRIDGE, navigationDecision, readRuntimeEvent } from '../src/webview-policy.ts';

const origin = 'https://world.example';
const launchUrl = `${origin}/player/?manifest=${encodeURIComponent(`${origin}/worlds/sample/rev-1/world.json`)}&sha256=${'a'.repeat(64)}`;
const context = { sourceUrl: launchUrl, launchUrl, worldId: 'sample', revisionId: 'rev-1' };
const close = { protocolVersion: 1, type: 'WorldClosed', requestId: 'web-close-2', worldId: 'sample', revisionId: 'rev-1', message: 'Closed' };
const envelope = (event: unknown, pageUrl = launchUrl) => JSON.stringify({ pageUrl, event });

test('only same-origin navigation is allowed and the server home exits the WebView', () => {
  assert.equal(navigationDecision(launchUrl, origin), 'allow');
  assert.equal(navigationDecision(`${origin}/`, origin), 'home');
  for (const raw of ['https://outside.example/', 'https://world.example:8443/player/', 'http://world.example/player/', 'https://world.example@outside.example/', 'https://user:password@world.example/player/', 'tel:123', 'intent://scan', 'javascript:alert(1)', 'about:blank', 'file:///sdcard/file.html', 'data:text/html,hello']) assert.equal(navigationDecision(raw, origin), 'block', raw);
});
test('correct player closure and RuntimeReady messages are accepted', () => {
  assert.equal(readRuntimeEvent(envelope(close), context)?.type, 'WorldClosed');
  assert.equal(readRuntimeEvent(envelope({ protocolVersion: 1, type: 'RuntimeReady', requestId: '' }), context)?.type, 'RuntimeReady');
});
test('a delayed closure from another world or revision cannot close the current world', () => {
  for (const payload of [{ ...close, worldId: 'other' }, { ...close, revisionId: 'rev-0' }, { ...close, requestId: '' }, { ...close, requestId: 'x'.repeat(129) }, { ...close, requestId: 'invalid id' }]) assert.equal(readRuntimeEvent(envelope(payload), context), null);
});
test('events must originate from the exact currently opened player URL', () => {
  for (const sourceUrl of ['https://other.example/', `${origin}/`, launchUrl.replace('rev-1', 'rev-0'), 'about:blank']) assert.equal(readRuntimeEvent(envelope(close), { ...context, sourceUrl }), null);
  for (const pageUrl of [`${origin}/`, launchUrl.replace('rev-1', 'rev-0'), 'https://outside.example/player/']) assert.equal(readRuntimeEvent(envelope(close, pageUrl), context), null);
});

test('Android origin-only callbacks require both the trusted native origin and exact injected page URL', () => {
  const android = { ...context, allowOriginOnlySource: true };
  for (const sourceUrl of [origin, `${origin}/`, launchUrl]) assert.equal(readRuntimeEvent(envelope(close), { ...android, sourceUrl })?.type, 'WorldClosed');
  for (const sourceUrl of ['https://outside.example', `${origin}/other`, `${origin}/?other=1`, `${origin}/#other`, 'https://user:password@world.example']) assert.equal(readRuntimeEvent(envelope(close), { ...android, sourceUrl }), null);
  assert.equal(readRuntimeEvent(envelope(close, `${origin}/other`), { ...android, sourceUrl: origin }), null);
  assert.equal(readRuntimeEvent(envelope(close), { ...context, sourceUrl: origin }), null);
});

test('injected bridge forwards the current document URL and installs only one listener', () => {
  const messages: string[] = [], listeners: ((event: { detail: unknown }) => void)[] = [];
  const window = {
    location: { href: launchUrl },
    ReactNativeWebView: { postMessage: (message: string) => messages.push(message) },
    addEventListener: (name: string, callback: (event: { detail: unknown }) => void) => { assert.equal(name, 'kimchily-world-event'); listeners.push(callback); },
  };
  runInNewContext(NATIVE_WORLD_BRIDGE, { window });
  runInNewContext(NATIVE_WORLD_BRIDGE, { window });
  assert.equal(listeners.length, 1);
  listeners[0]({ detail: close });
  assert.equal(readRuntimeEvent(messages[0], { ...context, sourceUrl: origin, allowOriginOnlySource: true })?.type, 'WorldClosed');
  window.location.href = `${origin}/other`;
  listeners[0]({ detail: close });
  assert.equal(readRuntimeEvent(messages[1], { ...context, sourceUrl: origin, allowOriginOnlySource: true }), null);
});

test('the actual web host command identities fit progress, failure, cancellation and both close paths', () => {
  const require = createRequire(import.meta.url);
  const { createHost, parseLaunch } = require('../../KimchilyUnityRuntime/Assets/WebGLTemplates/KimchilyWeb/host.js');
  const commands: Record<string, unknown>[] = [];
  const host = createHost(parseLaunch(launchUrl), () => {});
  host.start();
  host.attach({ SendMessage: (_target: string, _method: string, raw: string) => commands.push(JSON.parse(raw)) });
  const initialization = { ...commands[0], type: 'RuntimeReady', worldId: '', revisionId: '' };
  assert.equal(readRuntimeEvent(envelope(initialization), context)?.type, 'RuntimeReady');
  host.receive(initialization);
  const opening = commands[1];
  for (const type of ['WorldProgress', 'WorldReady', 'WorldFailed']) assert.equal(readRuntimeEvent(envelope({ ...opening, type, code: type === 'WorldFailed' ? 'LOAD_FAILED' : '' }), context)?.type, type);
  host.close();
  assert.equal(readRuntimeEvent(envelope({ ...opening, type: 'WorldFailed', code: 'CANCELLED' }), context)?.code, 'CANCELLED');
  assert.equal(readRuntimeEvent(envelope({ ...commands[2], type: 'WorldClosed' }), context)?.type, 'WorldClosed');
  assert.equal(readRuntimeEvent(envelope({ ...opening, type: 'WorldClosed', requestId: `unity-close-${'a'.repeat(32)}` }), context)?.type, 'WorldClosed');
});
test('malformed, oversized, unknown, and wrong-version events are ignored', () => {
  for (const raw of ['{', 'x'.repeat(17000), envelope({ ...close, protocolVersion: 2 }), envelope({ ...close, type: 'Execute' }), JSON.stringify(close), 'null']) assert.equal(readRuntimeEvent(raw, context), null);
});
