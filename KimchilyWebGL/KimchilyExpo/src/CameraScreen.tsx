import React, { useEffect, useRef, useState } from 'react';
import { AppState, BackHandler, Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { SafeAreaView } from 'react-native-safe-area-context';

interface Props { onScanned: (raw: string) => void; onCancel: () => void; onError: (message: string) => void }
export default function CameraScreen({ onScanned, onCancel, onError }: Props) {
  const [permission, requestPermission, getPermission] = useCameraPermissions();
  const [active, setActive] = useState(AppState.currentState === 'active');
  const [captured, setCaptured] = useState(false);
  const [requesting, setRequesting] = useState(false);
  const permissionPending = useRef(false);
  const locked = useRef(false), mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    const app = AppState.addEventListener('change', state => {
      setActive(state === 'active');
      if (state === 'active') void getPermission().catch(() => { /* The existing permission UI stays available. */ });
    });
    const back = BackHandler.addEventListener('hardwareBackPress', () => { onCancel(); return true; });
    return () => { mounted.current = false; app.remove(); back.remove(); };
  }, [onCancel, getPermission]);
  const askPermission = async () => {
    if (permissionPending.current) return;
    permissionPending.current = true; setRequesting(true);
    try { await requestPermission(); } catch { if (mounted.current) onError('카메라 권한을 요청하지 못했어요. 설정에서 권한을 확인해 주세요.'); }
    finally { permissionPending.current = false; if (mounted.current) setRequesting(false); }
  };
  return <SafeAreaView style={styles.root}>
    <View style={styles.header}><Pressable accessibilityRole="button" onPress={onCancel} style={styles.back}><Text style={styles.backText}>‹ 돌아가기</Text></Pressable><Text style={styles.heading}>월드 QR 스캔</Text><View style={{ width: 82 }} /></View>
    {permission?.granted ? <View style={styles.preview}>
      {active && !captured && <CameraView style={StyleSheet.absoluteFill} facing="back" barcodeScannerSettings={{ barcodeTypes: ['qr'] }} onMountError={() => { if (mounted.current && !locked.current) { locked.current = true; setCaptured(true); onError('카메라를 열지 못했어요. 다른 앱이 사용 중인지 확인하고 다시 시도해 주세요.'); } }} onBarcodeScanned={result => { if (locked.current || !mounted.current || AppState.currentState !== 'active') return; locked.current = true; setCaptured(true); onScanned(result.data); }} />}
      <View pointerEvents="none" style={styles.guide}><View style={styles.frame} /><Text style={styles.guideText}>{active ? 'QR 코드를 사각형 안에 맞춰 주세요' : '앱으로 돌아오면 카메라가 다시 켜져요'}</Text></View>
    </View> : <View style={styles.permission}><Text style={styles.permissionIcon}>⌗</Text><Text style={styles.permissionTitle}>월드 QR를 읽어 볼까요?</Text><Text style={styles.permissionBody}>카메라는 QR를 읽는 동안만 사용해요.{"\n"}촬영한 영상은 저장하거나 서버에 보내지 않아요.</Text><Pressable accessibilityRole="button" style={styles.permissionButton} disabled={requesting} onPress={() => { if (permission?.canAskAgain === false) void Linking.openSettings().catch(() => { if (mounted.current) onError('기기 설정에서 Kimchily 카메라 권한을 켜 주세요.'); }); else void askPermission(); }}><Text style={styles.permissionButtonText}>{permission?.canAskAgain === false ? '기기 설정 열기' : '카메라 사용 허용'}</Text></Pressable><Pressable accessibilityRole="button" onPress={onCancel} style={styles.cancel}><Text style={styles.cancelText}>홈으로 돌아갈게요</Text></Pressable></View>}
    <Text style={styles.footer}>연결된 Kimchily 서버의 월드 QR를 읽어 주세요.</Text>
  </SafeAreaView>;
}
const styles = StyleSheet.create({ root: { flex: 1, backgroundColor: '#102c2a' }, header: { padding: 13, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }, back: { paddingVertical: 12, paddingHorizontal: 8 }, backText: { color: '#c6f1dc', fontSize: 13 }, heading: { color: '#f0f8f0', fontWeight: '700', fontSize: 16 }, preview: { flex: 1, margin: 18, borderRadius: 23, overflow: 'hidden', backgroundColor: '#254942' }, guide: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 25 }, frame: { width: 230, height: 230, borderWidth: 3, borderColor: '#c6f1dc', borderRadius: 24 }, guideText: { marginTop: 28, color: '#fff', fontSize: 13, textAlign: 'center', backgroundColor: '#153131bb', padding: 12, borderRadius: 10 }, permission: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 30 }, permissionIcon: { color: '#c6f1dc', fontSize: 60, marginBottom: 25 }, permissionTitle: { color: '#f0f8f0', fontSize: 22, fontWeight: '700' }, permissionBody: { color: '#b4cbbd', textAlign: 'center', lineHeight: 24, marginTop: 18, marginBottom: 25, fontSize: 13 }, permissionButton: { padding: 17, backgroundColor: '#c6f1dc', borderRadius: 13 }, permissionButtonText: { color: '#153131', fontWeight: '700' }, cancel: { padding: 20 }, cancelText: { color: '#b4cbbd', fontSize: 12 }, footer: { textAlign: 'center', paddingHorizontal: 22, paddingVertical: 18, color: '#a0bcb0', fontSize: 11 }
});
