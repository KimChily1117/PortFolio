import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, BackHandler, KeyboardAvoidingView, Modal, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View, useWindowDimensions } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider, SafeAreaView } from 'react-native-safe-area-context';
import CameraScreen from './src/CameraScreen';
import WorldPlayer from './src/WorldPlayer';
import { fetchWorldCatalog, normalizeServerOrigin, resolveWorldQr, type WorldPublication } from './src/world-client';
import { appendRecent, parseRecents, RECENT_KEY, SERVER_KEY, visitLabel, type RecentWorld } from './src/app-state';

const clientOptions = { allowLanHttp: __DEV__ || Constants.expoConfig?.extra?.allowLanHttp === true };
const initialServer = process.env.EXPO_PUBLIC_WORLD_SERVER_URL || '';
type Sheet = 'server' | 'link' | null;

function Button({ title, onPress, secondary = false, disabled = false }: { title: string; onPress: () => void; secondary?: boolean; disabled?: boolean }) {
  return <Pressable accessibilityRole="button" disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.button, secondary && styles.secondaryButton, (disabled || pressed) && styles.dim]}><Text style={[styles.buttonText, secondary && styles.secondaryButtonText]}>{title}</Text></Pressable>;
}

function HomeApp() {
  const { width } = useWindowDimensions();
  const [hydrated, setHydrated] = useState(false);
  const [server, setServer] = useState('');
  const [draftServer, setDraftServer] = useState('');
  const [worlds, setWorlds] = useState<WorldPublication[]>([]);
  const [recents, setRecents] = useState<RecentWorld[]>([]);
  const [loading, setLoading] = useState(false);
  const [entryBusy, setEntryBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [sheet, setSheet] = useState<Sheet>(null);
  const [sheetError, setSheetError] = useState('');
  const [rawLink, setRawLink] = useState('');
  const [camera, setCamera] = useState(false);
  const [activeWorld, setActiveWorld] = useState<WorldPublication | null>(null);
  const aliases = useRef<string[]>([]);
  const catalogRequest = useRef<AbortController | null>(null);
  const entryRequest = useRef<AbortController | null>(null);
  const historyVersion = useRef(0);

  useEffect(() => {
    let alive = true;
    void (async () => {
      let configured = initialServer;
      try { configured = (await AsyncStorage.getItem(SERVER_KEY)) || configured; } catch { /* A session without persistence remains usable. */ }
      let normalized = '';
      try { if (configured) normalized = normalizeServerOrigin(configured, clientOptions); } catch { /* An old invalid setting opens the server sheet. */ }
      if (!alive) return;
      setServer(normalized); setDraftServer(normalized || configured); setHydrated(true);
      if (!normalized) setSheet('server');
    })();
    return () => { alive = false; catalogRequest.current?.abort(); entryRequest.current?.abort(); };
  }, []);

  const refresh = useCallback(async () => {
    if (!server || !hydrated) return;
    catalogRequest.current?.abort();
    const request = new AbortController(); catalogRequest.current = request;
    setLoading(true); setError('');
    try {
      const catalog = await fetchWorldCatalog(server, { ...clientOptions, signal: request.signal });
      if (catalogRequest.current !== request || request.signal.aborted) return;
      aliases.current = catalog.linkOrigins; setWorlds(catalog.worlds);
    } catch (reason) {
      if (catalogRequest.current !== request || request.signal.aborted) return;
      setWorlds([]); setError(reason instanceof Error ? reason.message : '월드 목록을 불러오지 못했어요.');
    } finally { if (catalogRequest.current === request) { catalogRequest.current = null; setLoading(false); } }
  }, [server, hydrated]);

  useEffect(() => {
    if (!hydrated) return;
    let alive = true;
    setWorlds([]); setRecents([]); aliases.current = server ? [server] : [];
    const version = ++historyVersion.current;
    void AsyncStorage.getItem(RECENT_KEY).then(raw => { if (alive && version === historyVersion.current) setRecents(parseRecents(raw, server, clientOptions)); }).catch(() => {});
    void refresh();
    return () => { alive = false; catalogRequest.current?.abort(); };
  }, [server, hydrated, refresh]);

  const closeCamera = useCallback(() => setCamera(false), []);
  const exitWorld = useCallback(() => { setActiveWorld(null); setNotice('월드에서 나왔어요. 다음 공간도 만나 볼까요?'); }, []);
  const cancelEntry = useCallback(() => { entryRequest.current?.abort(); entryRequest.current = null; setEntryBusy(false); }, []);
  useEffect(() => {
    if (!entryBusy) return;
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => { cancelEntry(); return true; });
    return () => subscription.remove();
  }, [entryBusy, cancelEntry]);

  const enter = async (raw: string) => {
    if (entryRequest.current) return;
    setCamera(false);
    if (!server) { setDraftServer(''); setSheet('server'); return; }
    setSheet(null); setError(''); setNotice('');
    const request = new AbortController(); entryRequest.current = request; setEntryBusy(true);
    try {
      const world = await resolveWorldQr(server, raw, { ...clientOptions, allowedOrigins: aliases.current, signal: request.signal });
      if (entryRequest.current !== request || request.signal.aborted) return;
      const next = appendRecent(recents, world, server, clientOptions);
      historyVersion.current++;
      setRecents(next);
      void AsyncStorage.setItem(RECENT_KEY, JSON.stringify(next)).catch(() => {});
      setActiveWorld(world);
    } catch (reason) {
      if (entryRequest.current === request && !request.signal.aborted) setError(reason instanceof Error ? reason.message : '월드에 연결하지 못했어요.');
    } finally { if (entryRequest.current === request) { entryRequest.current = null; setEntryBusy(false); } }
  };

  const openSheet = (next: Sheet) => {
    setSheetError('');
    if (next === 'server') setDraftServer(server || initialServer);
    setSheet(next);
  };
  const saveServer = () => {
    try {
      const normalized = normalizeServerOrigin(draftServer, clientOptions);
      cancelEntry(); setSheet(null); setError(''); setNotice('');
      if (normalized === server) void refresh(); else setServer(normalized);
      void AsyncStorage.setItem(SERVER_KEY, normalized).catch(() => setNotice('이 기기에서 주소를 저장하지 못했어요. 이번 실행에서는 연결을 사용할 수 있어요.'));
    } catch (reason) { setSheetError(reason instanceof Error ? reason.message : '서버 주소를 확인해 주세요.'); }
  };

  if (!hydrated) return <SafeAreaView style={styles.loadingRoot}><ActivityIndicator color="#117b68" /><Text style={styles.loadingText}>Kimchily를 준비하고 있어요</Text></SafeAreaView>;
  if (activeWorld) return <><StatusBar style="light" /><WorldPlayer world={activeWorld} origin={server} onExit={exitWorld} /></>;
  if (camera) return <><StatusBar style="light" /><CameraScreen onCancel={closeCamera} onScanned={raw => void enter(raw)} onError={message => { setCamera(false); setError(message); }} /></>;

  return <SafeAreaView style={styles.root} edges={['top', 'left', 'right']}>
    <StatusBar style="dark" />
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.scroll} refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void refresh()} tintColor="#117b68" />}>
      <View style={styles.content}>
        <View style={styles.header}><View style={styles.brandRow}><View style={styles.logo}><Text style={styles.logoText}>k</Text></View><Text style={styles.brand}>kimchily<Text style={styles.brandDot}>.</Text></Text></View><Pressable accessibilityRole="button" accessibilityLabel="월드 서버 연결 설정" onPress={() => openSheet('server')} style={({ pressed }) => [styles.serverButton, pressed && styles.dim]}><View style={[styles.serverDot, !server && styles.serverDotOff]} /><Text style={styles.serverButtonText}>연결 설정</Text></Pressable></View>

        <View style={styles.hero}>
          <Text style={styles.eyebrow}>YOUR NEXT LITTLE WORLD</Text>
          <Text style={styles.heroTitle}>새로운 공간으로,{"\n"}<Text style={styles.heroAccent}>함께 들어가요.</Text></Text>
          <Text style={styles.heroDescription}>마음에 드는 월드를 고르고,{"\n"}나만의 이야기를 시작해 보세요.</Text>
          <View style={styles.heroButtons}><View style={styles.flex}><Button title="⌗  월드 QR 스캔" disabled={entryBusy} onPress={() => {
            if (!server) { openSheet('server'); return; }
            if (Platform.OS === 'web') { setNotice('웹에서는 게시 서버의 홈에서 QR를 스캔할 수 있어요. 여기서는 링크를 붙여넣어 주세요.'); openSheet('link'); }
            else { setError(''); setCamera(true); }
          }} /></View><View style={styles.flex}><Button title="링크로 입장 ↗" secondary disabled={entryBusy} onPress={() => openSheet(server ? 'link' : 'server')} /></View></View>
          <View style={styles.heroIllustration} accessibilityElementsHidden importantForAccessibility="no-hide-descendants"><View style={styles.orbit} /><View style={styles.orbitSecond} /><Text style={styles.spark}>✦</Text><View style={styles.island}><View style={styles.portal}><View style={styles.portalOpening}><Text style={styles.portalStar}>✦</Text></View></View><View style={styles.tree} /><View style={styles.smallTree} /></View><Text style={styles.artLabel}>A WORLD OF YOUR OWN</Text></View>
        </View>

        {!!notice && <View style={styles.notice} accessibilityLiveRegion="polite"><Text style={styles.noticeText}>{notice}</Text></View>}
        {!!error && <View style={styles.error} accessibilityLiveRegion="polite"><Text style={styles.errorTitle}>연결을 확인해 주세요</Text><Text style={styles.errorText}>{error}</Text><View style={styles.errorActions}><Pressable accessibilityRole="button" onPress={() => void refresh()} style={styles.textPress}><Text style={styles.linkText}>다시 불러오기</Text></Pressable><Pressable accessibilityRole="button" onPress={() => openSheet('server')} style={styles.textPress}><Text style={styles.linkText}>서버 설정</Text></Pressable></View></View>}

        {recents.length > 0 && <View style={styles.recentSection}><View style={styles.sectionHeading}><Text style={styles.recentTitle}>최근에 방문한 월드</Text><Pressable accessibilityRole="button" onPress={() => { historyVersion.current++; setRecents([]); void AsyncStorage.removeItem(RECENT_KEY).catch(() => {}); }} style={styles.textPress}><Text style={styles.subtleLink}>기록 지우기</Text></Pressable></View><ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.recents}>{recents.map(recent => <Pressable accessibilityRole="button" accessibilityLabel={`${recent.world.title} 다시 입장`} key={recent.world.worldId} style={({ pressed }) => [styles.recentCard, pressed && styles.dim]} onPress={() => void enter(recent.world.launchUrl)}><View style={styles.recentIcon}><Text style={styles.recentIconText}>↗</Text></View><View><Text numberOfLines={1} style={styles.recentName}>{recent.world.title}</Text><Text style={styles.recentDate}>{visitLabel(recent.visitedAt)}</Text></View></Pressable>)}</ScrollView></View>}

        <View style={styles.worldSection}>
          <View style={styles.sectionHeading}><View><Text style={styles.eyebrowSmall}>FIND YOUR NEXT STOP</Text><Text style={styles.sectionTitle}>지금 열려 있는 월드</Text></View><Pressable accessibilityRole="button" accessibilityLabel="월드 목록 새로고침" disabled={loading} onPress={() => void refresh()} style={styles.refresh}>{loading ? <ActivityIndicator size="small" color="#117b68" /> : <Text style={styles.refreshText}>↻</Text>}</Pressable></View>
          {!loading && !error && worlds.length === 0 && <View style={styles.empty}><Text style={styles.emptyIcon}>✦</Text><Text style={styles.emptyTitle}>{server ? '첫 번째 월드를 기다리고 있어요' : '월드 서버에 연결해 주세요'}</Text><Text style={styles.emptyText}>{server ? 'Creator에서 WebGL 월드를 게시하면 여기에 나타나요.\n전달받은 QR나 링크로도 입장할 수 있어요.' : '연결 설정에서 서버 주소를 입력하면\n게시된 월드를 볼 수 있어요.'}</Text></View>}
          <View style={styles.worldGrid}>{worlds.map((world, index) => <View key={world.worldId} style={[styles.worldCard, width > 750 && styles.worldCardWide]}>
            <View style={[styles.cardArt, { backgroundColor: ['#deecdc', '#e5e9d3', '#dfeaed', '#ede2d8'][index % 4] }]}><Text style={styles.cardBadge}>WEB WORLD</Text><View style={styles.cardOrbit} /><View style={styles.cardPortal}><Text style={styles.cardStar}>✦</Text></View></View>
            <View style={styles.cardBody}><Text style={styles.cardTitle}>{world.title}</Text><Text style={styles.cardDescription}>새로운 이야기가 기다리는 공간</Text><Pressable accessibilityRole="button" accessibilityLabel={`${world.title} 월드 입장`} disabled={entryBusy} style={({ pressed }) => [styles.enterButton, pressed && styles.dim]} onPress={() => void enter(world.launchUrl)}><Text style={styles.enterText}>월드 입장</Text><Text style={styles.enterArrow}>↗</Text></Pressable></View>
          </View>)}</View>
        </View>
        <View style={styles.footer}><Text style={styles.footerBrand}>kimchily.</Text><Text style={styles.footerText}>작은 공간에서 시작하는, 더 큰 이야기.</Text></View>
      </View>
    </ScrollView>

    <Modal visible={sheet !== null} transparent animationType="slide" onRequestClose={() => setSheet(null)}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.modalBackdrop}>
        <View style={styles.sheet}><ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.sheetContent}><View style={styles.sheetHeader}><Text style={styles.sheetTitle}>{sheet === 'server' ? '월드 서버 연결' : '링크로 입장'}</Text><Pressable accessibilityRole="button" accessibilityLabel="창 닫기" onPress={() => setSheet(null)} style={styles.close}><Text style={styles.closeText}>×</Text></Pressable></View>
          {sheet === 'server' ? <><Text style={styles.sheetDescription}>월드가 게시된 서버 주소를 입력해 주세요.</Text><Text style={styles.inputLabel}>서버 주소</Text><TextInput accessibilityLabel="월드 서버 주소" value={draftServer} onChangeText={setDraftServer} placeholder={clientOptions.allowLanHttp ? 'http://192.168.0.4:8788' : 'https://world.example.com'} autoCapitalize="none" autoCorrect={false} keyboardType="url" textContentType="URL" maxLength={256} style={styles.input} placeholderTextColor="#91a095" onSubmitEditing={saveServer} /><Text style={styles.inputHelp}>{clientOptions.allowLanHttp ? '같은 Wi-Fi의 PC 서버를 사용할 때는 PC의 LAN 주소를 입력하세요. localhost는 현재 휴대폰을 가리켜요.' : 'HTTPS로 운영 중인 월드 서버를 연결할 수 있어요.'}</Text>{!!sheetError && <Text style={styles.formError} accessibilityLiveRegion="polite">{sheetError}</Text>}<Button title="저장하고 연결" onPress={saveServer} /></> : <><Text style={styles.sheetDescription}>전달받은 Kimchily 월드 링크를 붙여넣어 주세요.</Text><TextInput accessibilityLabel="월드 링크" value={rawLink} onChangeText={setRawLink} multiline autoCapitalize="none" autoCorrect={false} placeholder="월드 링크 붙여넣기" placeholderTextColor="#91a095" maxLength={4096} style={[styles.input, styles.linkInput]} /><Text style={styles.inputHelp}>현재 연결된 서버에서 게시한 월드만 열 수 있어요.</Text><Button title="월드로 이동 ↗" disabled={!rawLink.trim()} onPress={() => void enter(rawLink)} /></>}
        </ScrollView></View>
      </KeyboardAvoidingView>
    </Modal>
    <Modal visible={entryBusy} transparent animationType="fade" onRequestClose={cancelEntry}><View style={styles.busyBackdrop}><View style={styles.busyCard}><ActivityIndicator color="#117b68" size="large" /><Text style={styles.busyTitle}>월드 연결을 확인하고 있어요</Text><Text style={styles.busyText}>잠시 후 시작 화면으로 이동해요.</Text><Pressable accessibilityRole="button" onPress={cancelEntry} style={styles.textPress}><Text style={styles.linkText}>취소</Text></Pressable></View></View></Modal>
  </SafeAreaView>;
}

export default function App() { return <SafeAreaProvider><HomeApp /></SafeAreaProvider>; }

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#f7f9f3' }, scroll: { flexGrow: 1, paddingBottom: 28 }, content: { width: '100%', maxWidth: 1000, alignSelf: 'center', paddingHorizontal: 24 }, flex: { flex: 1 }, dim: { opacity: .55 }, loadingRoot: { flex: 1, backgroundColor: '#f7f9f3', justifyContent: 'center', alignItems: 'center', gap: 15 }, loadingText: { color: '#647570', fontSize: 13 }, header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: 17, paddingBottom: 20, gap: 10 }, brandRow: { flexDirection: 'row', alignItems: 'center', gap: 8 }, logo: { backgroundColor: '#153131', borderRadius: 10, width: 30, height: 30, justifyContent: 'center', alignItems: 'center' }, logoText: { color: '#c6f1dc', fontSize: 27, fontWeight: '800', lineHeight: 30 }, brand: { color: '#153131', fontSize: 27, fontWeight: '800', letterSpacing: -1.2 }, brandDot: { color: '#117b68' }, serverButton: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 11, paddingVertical: 12, borderRadius: 20, borderColor: '#dce5dd', borderWidth: 1 }, serverDot: { width: 5, height: 5, borderRadius: 3, backgroundColor: '#78a281' }, serverDotOff: { backgroundColor: '#aeae8f' }, serverButtonText: { color: '#4a6859', fontSize: 10, fontWeight: '600' }, hero: { paddingTop: 24, borderBottomWidth: 1, borderBottomColor: '#dce5dd' }, eyebrow: { color: '#117b68', fontSize: 9, fontWeight: '700', letterSpacing: 1.7, marginBottom: 19 }, heroTitle: { fontSize: 38, lineHeight: 49, letterSpacing: -1.8, fontWeight: '700', color: '#153131' }, heroAccent: { color: '#117b68' }, heroDescription: { color: '#647570', fontSize: 13, lineHeight: 24, marginTop: 19, marginBottom: 25 }, heroButtons: { flexDirection: 'row', gap: 10 }, button: { backgroundColor: '#153131', borderRadius: 13, paddingVertical: 16, paddingHorizontal: 13, minHeight: 50, justifyContent: 'center', alignItems: 'center' }, buttonText: { color: '#fff', fontWeight: '700', fontSize: 13 }, secondaryButton: { backgroundColor: '#ffffff77', borderWidth: 1, borderColor: '#cedbd2' }, secondaryButtonText: { color: '#153131' }, heroIllustration: { height: 234, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' }, orbit: { position: 'absolute', width: 275, height: 159, borderRadius: 150, borderWidth: 1, borderColor: '#c9ddc6', transform: [{ rotate: '-23deg' }] }, orbitSecond: { position: 'absolute', width: 172, height: 236, borderRadius: 150, borderWidth: 1, borderColor: '#dce7d5', transform: [{ rotate: '-42deg' }] }, island: { width: 226, height: 98, borderRadius: 120, backgroundColor: '#a9ce95', marginTop: 50, borderBottomWidth: 13, borderColor: '#679873', transform: [{ rotate: '-5deg' }] }, portal: { position: 'absolute', bottom: 29, left: 84, width: 69, height: 108, backgroundColor: '#eaf2d7', borderTopLeftRadius: 38, borderTopRightRadius: 38, borderRightWidth: 7, borderColor: '#87ad84', padding: 9, paddingBottom: 0 }, portalOpening: { flex: 1, borderTopLeftRadius: 30, borderTopRightRadius: 30, backgroundColor: '#519480', justifyContent: 'center', alignItems: 'center' }, portalStar: { fontSize: 25, color: '#dbf6b5' }, tree: { position: 'absolute', bottom: 43, left: 24, width: 35, height: 57, borderRadius: 23, backgroundColor: '#4f9973' }, smallTree: { position: 'absolute', bottom: 36, right: 27, width: 28, height: 41, borderRadius: 20, backgroundColor: '#70a373' }, spark: { position: 'absolute', right: 14, top: 58, fontSize: 24, color: '#8eae81' }, artLabel: { position: 'absolute', bottom: 20, right: 0, color: '#7c9681', fontSize: 8, letterSpacing: 1.5 }, notice: { padding: 15, borderRadius: 12, backgroundColor: '#e9f0e4', marginTop: 21 }, noticeText: { color: '#526c5b', fontSize: 12, lineHeight: 21 }, error: { backgroundColor: '#fff0e7', borderRadius: 13, padding: 18, marginTop: 21 }, errorTitle: { color: '#804b32', fontSize: 14, fontWeight: '700', marginBottom: 7 }, errorText: { color: '#855a43', fontSize: 12, lineHeight: 21 }, errorActions: { flexDirection: 'row', gap: 20, marginTop: 4 }, textPress: { paddingVertical: 12, paddingHorizontal: 3 }, linkText: { color: '#117b68', fontSize: 12, fontWeight: '700' }, subtleLink: { color: '#6d8774', fontSize: 10 }, recentSection: { paddingTop: 26 }, sectionHeading: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10, marginBottom: 17 }, recentTitle: { color: '#153131', fontSize: 18, fontWeight: '700', letterSpacing: -.6 }, recents: { gap: 10, paddingBottom: 3 }, recentCard: { flexDirection: 'row', alignItems: 'center', gap: 11, padding: 12, borderWidth: 1, borderColor: '#dce5dd', borderRadius: 13, backgroundColor: '#ffffff99' }, recentIcon: { width: 36, height: 36, justifyContent: 'center', alignItems: 'center', backgroundColor: '#e6efe0', borderRadius: 10 }, recentIconText: { color: '#117b68', fontSize: 21 }, recentName: { maxWidth: 160, color: '#153131', fontSize: 12, fontWeight: '600' }, recentDate: { color: '#748675', fontSize: 10, marginTop: 5 }, worldSection: { paddingTop: 29, paddingBottom: 27 }, eyebrowSmall: { color: '#117b68', fontSize: 8, fontWeight: '700', letterSpacing: 1.4, marginBottom: 9 }, sectionTitle: { color: '#153131', fontWeight: '700', fontSize: 22, letterSpacing: -.9 }, refresh: { width: 41, height: 41, borderRadius: 11, borderColor: '#dce5dd', borderWidth: 1, justifyContent: 'center', alignItems: 'center' }, refreshText: { color: '#496d55', fontSize: 24 }, empty: { padding: 25, borderRadius: 17, borderWidth: 1, borderStyle: 'dashed', borderColor: '#cfdccf', alignItems: 'center' }, emptyIcon: { color: '#81a48c', fontSize: 28, marginBottom: 14 }, emptyTitle: { color: '#153131', fontWeight: '600', fontSize: 15, marginBottom: 10 }, emptyText: { textAlign: 'center', color: '#647570', fontSize: 11, lineHeight: 21 }, worldGrid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', gap: 18 }, worldCard: { width: '100%', backgroundColor: '#fff', borderRadius: 18, borderWidth: 1, borderColor: '#e0e7de', overflow: 'hidden' }, worldCardWide: { width: '48.7%' }, cardArt: { height: 165, justifyContent: 'center', alignItems: 'center', overflow: 'hidden' }, cardBadge: { position: 'absolute', left: 14, top: 15, color: '#58715c', fontSize: 8, letterSpacing: 1, backgroundColor: '#ffffffbb', padding: 7, borderRadius: 10 }, cardOrbit: { position: 'absolute', width: 245, height: 129, borderWidth: 1, borderColor: '#ffffffbb', borderRadius: 130, transform: [{ rotate: '-28deg' }] }, cardPortal: { width: 69, height: 92, borderColor: '#f7f8e7', borderWidth: 10, borderBottomWidth: 0, borderTopLeftRadius: 42, borderTopRightRadius: 42, backgroundColor: '#79ab8e', justifyContent: 'center', alignItems: 'center', transform: [{ rotate: '-8deg' }] }, cardStar: { color: '#e5f8cd', fontSize: 25 }, cardBody: { padding: 18 }, cardTitle: { fontSize: 18, fontWeight: '700', color: '#153131', letterSpacing: -.4 }, cardDescription: { color: '#647570', fontSize: 11, marginTop: 7, marginBottom: 18 }, enterButton: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', borderRadius: 10, paddingVertical: 12, paddingHorizontal: 14, backgroundColor: '#edf5ee' }, enterText: { color: '#117b68', fontWeight: '700', fontSize: 12 }, enterArrow: { color: '#117b68', fontSize: 21 }, footer: { borderTopWidth: 1, borderTopColor: '#dce5dd', paddingVertical: 24, flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 10 }, footerBrand: { color: '#52715a', fontSize: 19, fontWeight: '700', letterSpacing: -.8 }, footerText: { color: '#849486', fontSize: 9 }, modalBackdrop: { flex: 1, justifyContent: 'flex-end', alignItems: 'center', backgroundColor: '#112b2c99' }, sheet: { backgroundColor: '#f7f9f3', width: '100%', maxWidth: 580, maxHeight: '90%', borderTopLeftRadius: 25, borderTopRightRadius: 25, overflow: 'hidden' }, sheetContent: { padding: 26, paddingBottom: 45 }, sheetHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 13 }, sheetTitle: { color: '#153131', fontWeight: '700', fontSize: 23, letterSpacing: -.7 }, close: { height: 40, width: 40, alignItems: 'center', justifyContent: 'center', backgroundColor: '#e8eee2', borderRadius: 22 }, closeText: { fontSize: 27, color: '#627967' }, sheetDescription: { color: '#647570', fontSize: 13, lineHeight: 22, marginBottom: 23 }, inputLabel: { fontSize: 12, color: '#496353', marginBottom: 10, fontWeight: '600' }, input: { borderColor: '#cfdccf', borderWidth: 1, borderRadius: 12, backgroundColor: '#fff', color: '#153131', padding: 15, fontSize: 14, minHeight: 51 }, linkInput: { minHeight: 120, textAlignVertical: 'top' }, inputHelp: { fontSize: 11, color: '#758675', lineHeight: 19, marginTop: 12, marginBottom: 24 }, formError: { color: '#9a5438', lineHeight: 21, fontSize: 12, marginBottom: 17 }, busyBackdrop: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: '#f7f9f3ef', padding: 25 }, busyCard: { alignItems: 'center', gap: 19 }, busyTitle: { fontSize: 18, color: '#153131', fontWeight: '700' }, busyText: { fontSize: 13, color: '#647570' }
});
