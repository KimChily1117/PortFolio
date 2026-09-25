const MAX_FILE_BYTES = 15 * 1024 * 1024;
const MAX_IMAGE_PIXELS = 40000000;

function stopTracks(stream) { if (stream) for (const track of stream.getTracks()) track.stop(); }
function cameraError(error) {
  if (error?.name === 'NotAllowedError' || error?.name === 'SecurityError')
    return new Error('카메라 권한을 허용해 주세요. QR 이미지 파일로도 열 수 있습니다.');
  if (error?.name === 'NotFoundError') return new Error('카메라를 찾지 못했습니다. QR 이미지 파일을 선택해 주세요.');
  if (error?.name === 'NotReadableError') return new Error('다른 앱이 카메라를 사용 중일 수 있습니다. 잠시 후 다시 시도해 주세요.');
  return error instanceof Error ? error : new Error('카메라를 시작하지 못했습니다.');
}
function loadImage(file, signal) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    const url = URL.createObjectURL(file);
    const cleanup = () => { image.onload = null; image.onerror = null; signal.removeEventListener('abort', aborted); URL.revokeObjectURL(url); };
    const aborted = () => { cleanup(); image.src = ''; reject(new DOMException('Cancelled', 'AbortError')); };
    image.onload = () => { cleanup(); resolve(image); };
    image.onerror = () => { cleanup(); reject(new Error('이미지를 읽지 못했습니다. PNG, JPEG 또는 WebP 파일을 사용해 주세요.')); };
    signal.addEventListener('abort', aborted, { once: true });
    if (signal.aborted) { aborted(); return; }
    image.src = url;
  });
}

/** Call start from a button gesture. Results are raw text; this module never navigates. */
export function createQrScanner({ video, onResult, onError = () => {}, onState = () => {}, environment = {} }) {
  if (!video || typeof onResult !== 'function') throw new Error('A video element and onResult callback are required.');
  const doc = environment.document || globalThis.document;
  const win = environment.window || globalThis.window;
  const media = environment.mediaDevices || globalThis.navigator?.mediaDevices;
  const raf = environment.requestAnimationFrame || globalThis.requestAnimationFrame?.bind(globalThis);
  const caf = environment.cancelAnimationFrame || globalThis.cancelAnimationFrame?.bind(globalThis);
  const canvas = environment.createCanvas ? environment.createCanvas() : doc.createElement('canvas');
  const context = canvas.getContext('2d', { willReadFrequently: true });
  const decode = environment.decode || ((data, width, height, options) => {
    if (typeof globalThis.jsQR !== 'function') throw new Error('QR 디코더를 불러오지 못했습니다. 페이지를 다시 열어 주세요.');
    return globalThis.jsQR(data, width, height, options);
  });
  let epoch = 0, stream = null, frame = null, fileAbort = null, disposed = false, state = 'stopped';
  function setState(value) { state = value; onState(value); }
  function stop() {
    epoch++;
    if (frame !== null) { caf(frame); frame = null; }
    if (fileAbort) { fileAbort.abort(); fileAbort = null; }
    stopTracks(stream); stream = null;
    video.pause(); video.srcObject = null;
    setState('stopped');
  }
  function deliver(text) { stop(); onResult(text); return text; }
  function pixels(source, width, height, maxSide, inversionAttempts) {
    if (!context) throw new Error('이 브라우저에서 이미지 읽기를 지원하지 않습니다.');
    const scale = Math.min(1, maxSide / Math.max(width, height));
    canvas.width = Math.max(1, Math.round(width * scale)); canvas.height = Math.max(1, Math.round(height * scale));
    context.drawImage(source, 0, 0, canvas.width, canvas.height);
    const image = context.getImageData(0, 0, canvas.width, canvas.height);
    return decode(image.data, image.width, image.height, { inversionAttempts });
  }
  async function start() {
    if (disposed) return false;
    stop(); const ownEpoch = epoch;
    const secure = environment.isSecureContext ?? globalThis.isSecureContext;
    if (!secure || !media?.getUserMedia) {
      onError(new Error('카메라는 HTTPS 연결에서 사용할 수 있습니다. 안내된 HTTPS 주소로 접속하거나 QR 이미지 파일을 선택해 주세요.'));
      return false;
    }
    setState('starting');
    try {
      const acquired = await media.getUserMedia({ audio: false, video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } } });
      // Permission prompts cannot be aborted. Close a late granted stream instead
      // of reviving a scanner the user already cancelled or replaced.
      if (ownEpoch !== epoch || disposed) { stopTracks(acquired); return false; }
      stream = acquired;
      video.muted = true; video.playsInline = true; video.setAttribute('playsinline', '');
      video.srcObject = acquired;
      await video.play();
      if (ownEpoch !== epoch || disposed) { stopTracks(acquired); return false; }
      setState('scanning'); let last = -Infinity, scans = 0;
      const tick = timestamp => {
        if (ownEpoch !== epoch || disposed) return;
        frame = null;
        try {
          if (timestamp - last >= 125 && video.readyState >= 2 && video.videoWidth > 0 && video.videoHeight > 0) {
            last = timestamp;
            const result = pixels(video, video.videoWidth, video.videoHeight, 960, (++scans % 4) === 0 ? 'attemptBoth' : 'dontInvert');
            if (result?.data) { deliver(result.data); return; }
          }
          frame = raf(tick);
        } catch (error) { stop(); onError(cameraError(error)); }
      };
      frame = raf(tick);
      return true;
    } catch (error) {
      if (ownEpoch !== epoch || disposed) return false;
      stop(); onError(cameraError(error)); return false;
    }
  }
  async function scanFile(file) {
    if (disposed) return null;
    stop(); const ownEpoch = epoch;
    if (!file || file.size <= 0 || file.size > MAX_FILE_BYTES || !/^image\/(png|jpeg|webp)$/i.test(file.type)) {
      onError(new Error('15MB 이하 PNG, JPEG 또는 WebP QR 이미지를 선택해 주세요.')); return null;
    }
    fileAbort = new AbortController(); setState('decoding');
    try {
      const image = await (environment.loadImage || loadImage)(file, fileAbort.signal);
      if (ownEpoch !== epoch || disposed) return null;
      const width = image.naturalWidth || image.width, height = image.naturalHeight || image.height;
      if (!(width > 0 && height > 0) || width * height > MAX_IMAGE_PIXELS) throw new Error('이미지가 너무 큽니다. QR 부분만 잘라 다시 선택해 주세요.');
      const result = pixels(image, width, height, 2048, 'attemptBoth');
      if (!result?.data) throw new Error('이미지에서 QR을 찾지 못했습니다. QR 전체가 선명하게 보이는 이미지를 선택해 주세요.');
      return deliver(result.data);
    } catch (error) {
      if (ownEpoch !== epoch || disposed || error?.name === 'AbortError') return null;
      stop(); onError(error); return null;
    }
  }
  const visibility = () => { if (doc.hidden) stop(); };
  const pagehide = () => stop();
  doc.addEventListener('visibilitychange', visibility);
  win?.addEventListener('pagehide', pagehide);
  return {
    start, stop, scanFile,
    dispose() { if (disposed) return; stop(); disposed = true; doc.removeEventListener('visibilitychange', visibility); win?.removeEventListener('pagehide', pagehide); },
    getState() { return state; }
  };
}
