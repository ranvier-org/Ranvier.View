
import { Operators_NullArg, Operators_IsNull } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/FSharp.Core.js";
import { CapabilityTasks_cancelled, CapabilityProvider, CapabilityObservation, CapabilityValue, Capabilities_unknown, CapabilityCategory, Capabilities_key, CapabilityFact, CapabilityState, Capabilities_create, CapabilityContextException } from "../Ranvier.Dom/Capabilities.js";
import { disposeSafe } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Util.js";
import { map, concat } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Array.js";
import { task } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/TaskBuilder.js";
import { isCancellationRequested, awaitPromise, startAsTask, throwIfCancellationRequested } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Async.js";

const images = ["image/png", "image/jpeg", "image/gif", "image/bmp", "image/x-icon", "image/webp", "image/avif", "image/jxl", "image/svg+xml"];

const fonts = ["truetype", "opentype", "woff", "woff2", "collection", "truetype-variations", "opentype-variations", "woff-variations", "woff2-variations"];

const facilities = ["webgl", "webgpu", "webxr", "video", "web-audio", "media-stream", "media-recorder", "web-codecs", "encrypted-media", "network", "crypto-digest", "crypto-other-algorithms", "indexeddb", "cache-storage", "service-worker", "shared-worker", "websocket", "webtransport", "file-system", "push", "webassembly", "clipboard", "notifications", "speech-synthesis", "gamepad"];

/**
 * Creates an extensible borrowed realm provider; pagehide or explicit disposal invalidates pending reports.
 */
export function forDocument(document$) {
    if (Operators_IsNull(document$)) {
        Operators_NullArg("document");
    }
    if (!(document$.defaultView != null && document$.defaultView.document === document$)) {
        throw new CapabilityContextException("Capability queries require an active document Window.");
    }
    const service = Capabilities_create();
    let disposed = false;
    const invalidate = () => {
        if (!disposed) {
            disposed = true;
            disposeSafe(service);
        }
    };
    const active = () => {
        if (disposed) {
            throw new CapabilityContextException("CapabilityService is disposed.");
        }
        if (!(document$.defaultView != null && document$.defaultView.document === document$)) {
            invalidate();
            throw new CapabilityContextException("The capability document is no longer active.");
        }
    };
    const fact = (reason, value) => (new CapabilityFact(value ? CapabilityState.Yes : CapabilityState.No, reason));
    const knownKeys = concat([map((name) => Capabilities_key(CapabilityCategory.ImageFormat, name), images), map((name_1) => Capabilities_key(CapabilityCategory.FontFormat, name_1), fonts), map((name_2) => Capabilities_key(CapabilityCategory.PlatformFacility, name_2), facilities)]);
    const builtin = service["Ranvier.Dom.CapabilityService.Register1271AB07"](new CapabilityProvider("browser-realm", knownKeys, (key, token) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            let matchValue, supported_1, Exported_1, Implemented_1, Exported_2, matchValue_1, Permitted, matchValue_2;
            active();
            throwIfCancellationRequested(token);
            let observation = Capabilities_unknown(key, "browser-realm", "passive realm observation", "current document realm", "No passive observation for this fact.");
            return builder$0040.Combine((matchValue = key.Category, (matchValue.tag === 2) ? ((typeof document$.defaultView.ImageDecoder?.isTypeSupported === 'function') ? builder$0040.Bind(startAsTask(awaitPromise(document$.defaultView.ImageDecoder.isTypeSupported(key.Name))), (_arg) => {
                let Exported, Implemented;
                const supported = _arg;
                active();
                throwIfCancellationRequested(token);
                observation = ((Exported = fact("Actual realm ImageDecoder.isTypeSupported function presence.", true), (Implemented = fact("ImageDecoder format-support query; HTML image support and actual decode/render remain unverified.", supported), new CapabilityObservation(observation.Key, Exported, Implemented, observation.Enabled, observation.Permitted, observation.Available, new CapabilityValue(/* Boolean */ 0, [supported]), observation.ProviderId, observation.Mechanism, "ImageDecoder.isTypeSupported / current document realm; HTML image rendering is separate."))));
                return builder$0040.Zero();
            }) : (builder$0040.Zero())) : ((matchValue.tag === 3) ? ((typeof document$.defaultView.CSS?.supports === 'function' && document$.defaultView.CSS.supports('font-format(woff)')) ? ((supported_1 = (document$.defaultView.CSS.supports('font-format(' + key.Name + ')')), (observation = ((Exported_1 = fact("CSS.supports accepts the font-format query grammar.", true), (Implemented_1 = fact("Actual realm CSS font-format support query; actual font loading remains unverified.", supported_1), new CapabilityObservation(observation.Key, Exported_1, Implemented_1, observation.Enabled, observation.Permitted, observation.Available, new CapabilityValue(/* Boolean */ 0, [supported_1]), observation.ProviderId, observation.Mechanism, observation.Scope)))), builder$0040.Zero()))) : (builder$0040.Zero())) : ((matchValue.tag === 4) ? ((observation = ((Exported_2 = ((matchValue_1 = ((() => { const w=document$.defaultView, n=w.navigator; switch(key.Name) { case 'webgl': return typeof w.WebGLRenderingContext !== 'undefined'; case 'webgpu': return 'gpu' in n; case 'webxr': return 'xr' in n; case 'video': return typeof w.HTMLVideoElement !== 'undefined'; case 'web-audio': return typeof w.AudioContext !== 'undefined'; case 'media-stream': return typeof w.MediaStream !== 'undefined'; case 'media-recorder': return typeof w.MediaRecorder !== 'undefined'; case 'web-codecs': return typeof w.VideoDecoder !== 'undefined'; case 'encrypted-media': return typeof n.requestMediaKeySystemAccess === 'function'; case 'network': return typeof w.fetch === 'function'; case 'crypto-digest': return typeof w.crypto?.subtle?.digest === 'function'; case 'crypto-other-algorithms': return typeof w.crypto?.subtle?.encrypt === 'function'; case 'indexeddb': return 'indexedDB' in w; case 'cache-storage': return 'caches' in w; case 'service-worker': return 'serviceWorker' in n; case 'shared-worker': return typeof w.SharedWorker !== 'undefined'; case 'websocket': return typeof w.WebSocket !== 'undefined'; case 'webtransport': return typeof w.WebTransport !== 'undefined'; case 'file-system': return typeof n.storage?.getDirectory === 'function'; case 'push': return typeof w.PushManager !== 'undefined'; case 'webassembly': return typeof w.WebAssembly !== 'undefined'; case 'clipboard': return 'clipboard' in n; case 'notifications': return typeof w.Notification !== 'undefined'; case 'speech-synthesis': return 'speechSynthesis' in w; case 'gamepad': return typeof n.getGamepads === 'function'; default: return null; } })()), (matchValue_1 == null) ? observation.Exported : fact("Actual realm API/property presence; behavior is unverified.", matchValue_1))), (Permitted = ((matchValue_2 = ((() => { const p=document$.permissionsPolicy ?? document$.featurePolicy; const names = { 'webgpu': 'webgpu', 'webxr': 'xr-spatial-tracking', 'clipboard': 'clipboard-read', 'encrypted-media': 'encrypted-media' }; const n=names[key.Name]; if (!n || typeof p?.features !== 'function' || typeof p?.allowsFeature !== 'function' || !p.features().includes(n)) return null; return p.allowsFeature(n); })()), (matchValue_2 == null) ? observation.Permitted : fact("Current document Permission Policy allowsFeature result.", matchValue_2))), new CapabilityObservation(observation.Key, Exported_2, observation.Implemented, observation.Enabled, Permitted, observation.Available, observation.Value, observation.ProviderId, observation.Mechanism, observation.Scope)))), builder$0040.Zero())) : (builder$0040.Zero())))), builder$0040.Delay(() => builder$0040.Return(observation)));
        }));
    }));
    const pageHide = (() => { const w=document$.defaultView, f=() => invalidate(); w.addEventListener('pagehide',f); return { Dispose: () => w.removeEventListener('pagehide',f) }; })();
    return {
        "Ranvier.Dom.CapabilityService.KnownKeys"() {
            active();
            return service["Ranvier.Dom.CapabilityService.KnownKeys"]();
        },
        "Ranvier.Dom.CapabilityService.Register1271AB07"(provider) {
            active();
            return service["Ranvier.Dom.CapabilityService.Register1271AB07"](provider);
        },
        "Ranvier.Dom.CapabilityService.Observe5FB49CD3"(key_1, token_1) {
            const key_2 = Capabilities_key(key_1.Category, key_1.Name);
            if (isCancellationRequested(token_1)) {
                return CapabilityTasks_cancelled(token_1);
            }
            else {
                active();
                const builder$0040_1 = task();
                return builder$0040_1.Run(builder$0040_1.Delay(() => builder$0040_1.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](key_2, token_1), (_arg_1) => {
                    active();
                    throwIfCancellationRequested(token_1);
                    return builder$0040_1.Return(_arg_1);
                })));
            }
        },
        Dispose() {
            invalidate();
            disposeSafe(builtin);
            disposeSafe(pageHide);
        },
    };
}

