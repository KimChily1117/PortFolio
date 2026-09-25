import React, { useEffect, useState } from 'react';
import { ActivityIndicator, BackHandler, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { WebView } from 'react-native-webview';
import type { WorldPlayerProps } from './WorldPlayer.types';
import { NATIVE_WORLD_BRIDGE, navigationDecision, readRuntimeEvent } from './webview-policy';

export default function WorldPlayer({ world, origin, onExit }: WorldPlayerProps) {
  const insets = useSafeAreaInsets();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [status, setStatus] = useState('월드 시작을 눌러 입장하세요');
  useEffect(() => { const subscription = BackHandler.addEventListener('hardwareBackPress', () => { onExit(); return true; }); return () => subscription.remove(); }, [onExit]);
  return <View style={[styles.root, { paddingTop: insets.top, paddingLeft: insets.left, paddingRight: insets.right, paddingBottom: insets.bottom }]}>
    <View style={styles.toolbar}>
      <Pressable accessibilityRole="button" accessibilityLabel="월드에서 나가 홈으로 돌아가기" onPress={onExit} style={({ pressed }) => [styles.exit, pressed && styles.pressed]}><Text style={styles.exitText}>‹ 홈으로</Text></Pressable>
      <View style={styles.heading}><Text numberOfLines={1} style={styles.title}>{world.title}</Text><Text numberOfLines={1} style={styles.status}>{status}</Text></View>
      {loading && <ActivityIndicator color="#c6f1dc" size="small" />}
    </View>
    {error ? <View style={styles.error}><Text style={styles.errorTitle}>월드를 열지 못했어요</Text><Text style={styles.errorText}>{error}</Text><Pressable style={styles.retry} accessibilityRole="button" onPress={onExit}><Text style={styles.retryText}>홈으로 돌아가기</Text></Pressable></View> :
      <WebView
        key={world.launchUrl}
        style={styles.webview}
        source={{ uri: world.launchUrl }}
        // A narrow native whitelist opens unmatched URLs in the OS browser before
        // our callback. Pass all URLs through the strict callback instead.
        originWhitelist={['*']}
        javaScriptEnabled domStorageEnabled
        allowFileAccess={false} allowFileAccessFromFileURLs={false} allowUniversalAccessFromFileURLs={false}
        mixedContentMode="never" sharedCookiesEnabled={false} thirdPartyCookiesEnabled={false}
        setSupportMultipleWindows javaScriptCanOpenWindowsAutomatically={false}
        allowsInlineMediaPlayback allowsBackForwardNavigationGestures={false} allowsLinkPreview={false}
        injectedJavaScriptBeforeContentLoaded={NATIVE_WORLD_BRIDGE}
        injectedJavaScript={NATIVE_WORLD_BRIDGE}
        onOpenWindow={() => { /* Popups never open another browser or WebView. */ }}
        onShouldStartLoadWithRequest={request => {
          const action = navigationDecision(request.url, origin);
          if (action === 'home') { onExit(); return false; }
          return action === 'allow';
        }}
        onMessage={event => {
          const message = readRuntimeEvent(event.nativeEvent.data, { sourceUrl: event.nativeEvent.url, launchUrl: world.launchUrl, worldId: world.worldId, revisionId: world.revisionId, allowOriginOnlySource: Platform.OS === 'android' });
          if (!message) return;
          if (message.type === 'WorldClosed') onExit();
          else if (message.type === 'WorldReady') { setStatus('월드에 입장했어요'); setLoading(false); }
          else if (message.type === 'WorldProgress') setStatus('월드를 준비하고 있어요');
          else if (message.type === 'WorldFailed' && message.code !== 'CANCELLED') { setError(message.message || '월드 입장에 실패했어요. 홈에서 다시 시도해 주세요.'); setLoading(false); }
        }}
        onLoadStart={() => setLoading(true)}
        onLoadEnd={() => setLoading(false)}
        onError={() => { setError('서버 연결을 확인해 주세요. 같은 Wi-Fi를 사용하는 개발 서버는 PC에서 켜져 있어야 해요.'); setLoading(false); }}
        onHttpError={event => { if (event.nativeEvent.url === world.launchUrl) { setError(`서버 응답을 확인할 수 없어요 (${event.nativeEvent.statusCode}).`); setLoading(false); } }}
        onContentProcessDidTerminate={() => setError('월드를 실행하던 화면이 종료됐어요. 홈에서 다시 입장해 주세요.')}
        onRenderProcessGone={() => setError('월드를 실행할 메모리가 부족하거나 화면이 종료됐어요. 홈에서 다시 입장해 주세요.')}
      />}
  </View>;
}
const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#153131' }, toolbar: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 12, minHeight: 57, gap: 14 }, exit: { paddingHorizontal: 12, paddingVertical: 13, borderRadius: 10, backgroundColor: '#284942' }, exitText: { color: '#e8f7ec', fontSize: 13, fontWeight: '600' }, pressed: { opacity: .65 }, heading: { flex: 1 }, title: { color: '#f0f8f0', fontSize: 13, fontWeight: '600' }, status: { color: '#a4c4b9', fontSize: 10, marginTop: 4 }, webview: { flex: 1, backgroundColor: '#10282b' }, error: { flex: 1, justifyContent: 'center', padding: 32, gap: 16, alignItems: 'center' }, errorTitle: { fontSize: 20, fontWeight: '700', color: '#f0f8f0' }, errorText: { textAlign: 'center', color: '#b7cfc3', lineHeight: 23, maxWidth: 430 }, retry: { marginTop: 12, backgroundColor: '#c6f1dc', padding: 16, borderRadius: 12 }, retryText: { color: '#153131', fontWeight: '700' }
});
