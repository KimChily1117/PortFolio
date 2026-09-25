import com.google.zxing.BinaryBitmap;
import com.google.zxing.DecodeHintType;
import com.google.zxing.RGBLuminanceSource;
import com.google.zxing.Result;
import com.google.zxing.common.HybridBinarizer;
import com.google.zxing.qrcode.QRCodeReader;
import java.awt.image.BufferedImage;
import java.io.File;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.util.EnumMap;
import java.util.Map;
import javax.imageio.ImageIO;

/** Java helper. Decodes with the same ZXing core as the Android scanner. */
class DecodeQr {
    public static void main(String[] args) throws Exception {
        if (args.length < 1) throw new IllegalArgumentException("Expected a QR image path.");
        BufferedImage image = ImageIO.read(new File(args[0]));
        if (image == null) throw new IllegalArgumentException("ImageIO could not read the image.");
        int width = image.getWidth();
        int height = image.getHeight();
        int[] pixels = image.getRGB(0, 0, width, height, null, 0, width);
        RGBLuminanceSource source = new RGBLuminanceSource(width, height, pixels);
        Map<DecodeHintType, Object> hints = new EnumMap<>(DecodeHintType.class);
        hints.put(DecodeHintType.CHARACTER_SET, "UTF-8");
        if (java.util.Arrays.asList(args).contains("--try-harder")) hints.put(DecodeHintType.TRY_HARDER, true);
        Result result = new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)), hints);
        String decoded = result.getText();
        if (args.length >= 2 && args[1].equals("--verify-world-link")) {
            Class<?> parser = Class.forName("com.kimchily.app.WorldLink");
            Object target = parser.getMethod("parse", String.class, boolean.class).invoke(
                parser.getField("INSTANCE").get(null), decoded,
                args.length >= 3 && args[2].equals("--allow-http"));
            Class<?> type = target.getClass();
            System.err.println("WORLD_LINK_OK\t" + type.getMethod("getWorldId").invoke(target) + "\t" +
                type.getMethod("getRevisionId").invoke(target) + "\t" +
                type.getMethod("getManifestUrl").invoke(target) + "\t" +
                type.getMethod("getManifestSha256").invoke(target));
        }
        OutputStreamWriter writer = new OutputStreamWriter(System.out, StandardCharsets.UTF_8);
        writer.write(decoded);
        writer.flush();
    }
}
