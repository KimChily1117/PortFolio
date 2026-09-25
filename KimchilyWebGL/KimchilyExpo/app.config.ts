import type { ExpoConfig, ConfigContext } from 'expo/config';

export default ({ config }: ConfigContext): ExpoConfig => {
  const allowLanHttp = process.env.KIMCHILY_ALLOW_LAN_HTTP === '1';
  return {
    ...config,
    name: 'Kimchily', slug: 'kimchily-mobile', owner: 'kimchily', version: '0.1.0',
    orientation: 'default', userInterfaceStyle: 'light', scheme: 'kimchily-mobile',
    ios: {
      bundleIdentifier: 'com.kimchily.mobile', supportsTablet: true,
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false,
        NSLocalNetworkUsageDescription: '같은 Wi-Fi의 Kimchily 월드 서버에 연결합니다.',
        // Explicitly override Expo's introspection/native template default in
        // production; omitting ATS can leave NSAllowsArbitraryLoads enabled.
        NSAppTransportSecurity: { NSAllowsArbitraryLoads: allowLanHttp }
      }
    },
    android: { package: 'com.kimchily.mobile' },
    plugins: [
      ['expo-camera', { cameraPermission: '월드 QR 코드를 읽기 위해 카메라를 사용합니다.', recordAudioAndroid: false, barcodeScannerEnabled: true }],
      ['expo-build-properties', { android: { usesCleartextTraffic: allowLanHttp } }]
    ],
    extra: {
      ...config.extra, allowLanHttp,
      eas: { projectId: process.env.EAS_PROJECT_ID || '5f59c8b9-6991-4594-9e23-b4c861d2b5e7' }
    }
  };
};
