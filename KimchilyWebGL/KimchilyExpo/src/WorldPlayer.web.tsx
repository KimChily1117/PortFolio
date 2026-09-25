import React from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import type { WorldPlayerProps } from './WorldPlayer.types';
import { navigationDecision } from './webview-policy';

/** React Native WebView has no web implementation. Open the separately served web player. */
export default function WorldPlayer({ world, origin, onExit }: WorldPlayerProps) {
  return <View style={styles.root}><Text style={styles.brand}>kimchily.</Text><Text style={styles.title}>{world.title}</Text><Text style={styles.description}>웹에서는 월드 플레이어를 브라우저에서 열어요.</Text><Pressable accessibilityRole="link" style={styles.button} onPress={() => { if (navigationDecision(world.launchUrl, origin) === 'allow') void Linking.openURL(world.launchUrl); }}><Text style={styles.buttonText}>브라우저에서 월드 열기 ↗</Text></Pressable><Pressable accessibilityRole="button" style={styles.back} onPress={onExit}><Text style={styles.backText}>홈으로 돌아가기</Text></Pressable></View>;
}
const styles = StyleSheet.create({ root: { flex: 1, padding: 30, justifyContent: 'center', alignItems: 'center', backgroundColor: '#f7f9f3' }, brand: { fontWeight: '800', fontSize: 30, color: '#117b68', marginBottom: 30 }, title: { fontWeight: '700', fontSize: 24, color: '#153131' }, description: { fontSize: 14, color: '#647570', marginVertical: 20, textAlign: 'center' }, button: { padding: 18, borderRadius: 12, backgroundColor: '#153131' }, buttonText: { color: '#fff', fontWeight: '600' }, back: { padding: 20 }, backText: { color: '#117b68' } });
