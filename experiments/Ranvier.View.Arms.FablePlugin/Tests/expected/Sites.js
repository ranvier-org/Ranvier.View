
import { Signal$1__get_Value } from "<core>/Core.js";
import { Builders_Component_template } from "./src/Ranvier.View/Dsl.js";
import { Elements_section$1_$ctor_Z10189819, Elements_i$1_$ctor_Z10189819, Elements_div$1_$ctor_69400C3C, Elements_pre$1_$ctor_D6A77C0, Elements_Element$2__Yield_Z5833CC7, Elements_b$1_$ctor_Z10189819, Elements_Element$2__Yield_Z721C83C5, Elements_span$1_$ctor_Z7EAF50F5, Elements_Element$2__Run_706D0133 } from "./src/Ranvier.View/Generated/HtmlDsl.js";
import { Arms_Match_7CDE3892 } from "./Ranvier.View.Arms/Arms.js";
import { values } from "./Ranvier.View.Arms/Site.js";
import { Exception, int32ToString } from "./fable_modules/fable-library-js/Util.js";
import { TemplateParts_exit } from "./src/Ranvier.View/Template.js";
import { printf, toFail } from "./fable_modules/fable-library-js/String.js";
import { toStringOrEmpty } from "./fable_modules/fable-library-js/Types.js";
import { append } from "./fable_modules/fable-library-js/Array.js";

export function read(state) {
    return Signal$1__get_Value(state);
}

export const rich = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1, builder$0040_2, builder$0040_3, builder$0040_4, builder$0040_5;
    const armsTag_S1 = {};
    return [(armsInstance_S1, _arg_1) => {
        let matchResult, percent, count_1, text_1, count_2, text_2;
        switch (_arg_1.tag) {
            case 1: {
                if (_arg_1.fields[0] === 0) {
                    matchResult = 0;
                }
                else {
                    matchResult = 1;
                    percent = _arg_1.fields[0];
                }
                break;
            }
            case 2: {
                if (_arg_1.fields[1] === 0) {
                    matchResult = 2;
                }
                else if (_arg_1.fields[1] > 99) {
                    matchResult = 3;
                    count_1 = _arg_1.fields[1];
                    text_1 = _arg_1.fields[0];
                }
                else {
                    matchResult = 4;
                    count_2 = _arg_1.fields[1];
                    text_2 = _arg_1.fields[0];
                }
                break;
            }
            default:
                matchResult = 0;
        }
        switch (matchResult) {
            case 0:
                return [0, armsTag_S1, _arg_1, armsInstance_S1];
            case 1:
                return [1, armsTag_S1, percent, _arg_1, armsInstance_S1];
            case 2:
                return [2, armsTag_S1, _arg_1, armsInstance_S1];
            case 3:
                return [3, armsTag_S1, count_1, text_1, _arg_1, armsInstance_S1];
            default:
                return [4, armsTag_S1, count_2, text_2, _arg_1, armsInstance_S1];
        }
    }, [(builder$0040_1 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_1, Elements_Element$2__Yield_Z721C83C5(builder$0040_1, "Not started"))), (builder$0040_2 = Elements_b$1_$ctor_Z10189819(), Elements_Element$2__Run_706D0133(builder$0040_2, Elements_Element$2__Yield_Z5833CC7(builder$0040_2, (armsArm_S1n1_S1x4) => {
        const armsRead_S1n1 = values(armsArm_S1n1_S1x4, armsTag_S1);
        return int32ToString(armsRead_S1n1[2]);
    }))), (builder$0040_3 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_3, Elements_Element$2__Yield_Z721C83C5(builder$0040_3, "Empty"))), (builder$0040_4 = Elements_b$1_$ctor_Z10189819(), Elements_Element$2__Run_706D0133(builder$0040_4, Elements_Element$2__Yield_Z5833CC7(builder$0040_4, (armsArm_S1n2_S1x5) => {
        const armsRead_S1n2 = values(armsArm_S1n2_S1x5, armsTag_S1);
        return armsRead_S1n2[3];
    }))), (builder$0040_5 = Elements_pre$1_$ctor_D6A77C0(), Elements_Element$2__Run_706D0133(builder$0040_5, Elements_Element$2__Yield_Z5833CC7(builder$0040_5, (armsArm_S1n3_S1x5) => {
        const armsRead_S1n3 = values(armsArm_S1n3_S1x5, armsTag_S1);
        const count_2_S1n3 = armsRead_S1n3[2] | 0;
        return armsRead_S1n3[3] + int32ToString(count_2_S1n3);
    })))]];
})())]));

export const incomplete = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1, builder$0040_2;
    const armsTag_S2 = {};
    return [(armsInstance_S2, _arg_1) => {
        switch (_arg_1.tag) {
            case 0:
                return [0, armsTag_S2, _arg_1, armsInstance_S2];
            case 1:
                return [1, armsTag_S2, _arg_1.fields[0], _arg_1, armsInstance_S2];
            default:
                throw new Exception("Match failure: Fixtures.Types.Load");
        }
    }, [(builder$0040_1 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_1, Elements_Element$2__Yield_Z721C83C5(builder$0040_1, "Not started"))), (builder$0040_2 = Elements_b$1_$ctor_Z10189819(), Elements_Element$2__Run_706D0133(builder$0040_2, Elements_Element$2__Yield_Z5833CC7(builder$0040_2, (armsArm_S2n1_S2x4) => {
        const armsRead_S2n1 = values(armsArm_S2n1_S2x4, armsTag_S2);
        return int32ToString(armsRead_S2n1[2]);
    })))]];
})())]));

export const held = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1, builder$0040_2;
    const armsTag_S3 = {};
    return [(armsInstance_S3, _arg_1) => ((_arg_1.tag === 2) ? [0, armsTag_S3, _arg_1.fields[0], _arg_1, armsInstance_S3] : [1, armsTag_S3, _arg_1, armsInstance_S3]), [(builder$0040_1 = Elements_span$1_$ctor_Z7EAF50F5(TemplateParts_exit((armsArm_S3n1_S3x4, _arg_3, finish) => {
        const armsRead_S3n1 = values(armsArm_S3n1_S3x4, armsTag_S3);
        if (armsRead_S3n1[2] === "") {
            finish();
        }
        return () => {
        };
    })), Elements_Element$2__Run_706D0133(builder$0040_1, Elements_Element$2__Yield_Z721C83C5(builder$0040_1, "held"))), (builder$0040_2 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_2, Elements_Element$2__Yield_Z721C83C5(builder$0040_2, "Other")))]];
})())]));

export const bodies = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1, builder$0040_2, builder$0040_3, builder$0040_4, builder$0040_5;
    const armsTag_S4 = {};
    return [(armsInstance_S4, _arg_1) => ((_arg_1.tag === 2) ? ((_arg_1.fields[1] > 100) ? ((_arg_1.fields[1] > 1000) ? [0, armsTag_S4, _arg_1.fields[1], _arg_1, armsInstance_S4] : [1, armsTag_S4, _arg_1.fields[1], _arg_1, armsInstance_S4]) : ((_arg_1.fields[1] === 0) ? [2, armsTag_S4, _arg_1.fields[0], _arg_1.fields[0] + "!", _arg_1, armsInstance_S4] : [3, armsTag_S4, _arg_1.fields[0], _arg_1, armsInstance_S4])) : [4, armsTag_S4, _arg_1, armsInstance_S4]), [(builder$0040_1 = Elements_b$1_$ctor_Z10189819(), Elements_Element$2__Run_706D0133(builder$0040_1, Elements_Element$2__Yield_Z721C83C5(builder$0040_1, "huge"))), (builder$0040_2 = Elements_i$1_$ctor_Z10189819(), Elements_Element$2__Run_706D0133(builder$0040_2, Elements_Element$2__Yield_Z721C83C5(builder$0040_2, "big"))), (builder$0040_3 = Elements_pre$1_$ctor_D6A77C0(), Elements_Element$2__Run_706D0133(builder$0040_3, Elements_Element$2__Yield_Z5833CC7(builder$0040_3, (armsArm_S4n1_S4x5) => {
        const armsRead_S4n1 = values(armsArm_S4n1_S4x5, armsTag_S4);
        return armsRead_S4n1[3];
    }))), (builder$0040_4 = Elements_pre$1_$ctor_D6A77C0(), Elements_Element$2__Run_706D0133(builder$0040_4, Elements_Element$2__Yield_Z5833CC7(builder$0040_4, (armsArm_S4n2_S4x4) => {
        const armsRead_S4n2 = values(armsArm_S4n2_S4x4, armsTag_S4);
        return armsRead_S4n2[2];
    }))), (builder$0040_5 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_5, Elements_Element$2__Yield_Z721C83C5(builder$0040_5, "Other")))]];
})())]));

export const nested = Builders_Component_template((_arg, panel) => panel, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892((panel_1) => Signal$1__get_Value(panel_1.State), null, (() => {
    let armsTag_S5, builder$0040_2, builder$0040_3, builder$0040_4;
    const armsTag_S6 = {};
    return [(armsInstance_S6, _arg_1) => ((_arg_1.tag === 2) ? [0, armsTag_S6, _arg_1.fields[0], _arg_1, armsInstance_S6] : [1, armsTag_S6, _arg_1, armsInstance_S6]), [Elements_Element$2__Run_706D0133(Elements_section$1_$ctor_Z10189819(), [Arms_Match_7CDE3892((armsArm_S6n1_S6x4) => {
        const armsRead_S6n1 = values(armsArm_S6n1_S6x4, armsTag_S6);
        return Signal$1__get_Value(armsRead_S6n1[4].Open);
    }, null, (armsTag_S5 = ({}), [(armsInstance_S5, _arg_2) => (_arg_2 ? [0, armsTag_S5, _arg_2, armsInstance_S5] : [1, armsTag_S5, _arg_2, armsInstance_S5]), [(builder$0040_2 = Elements_pre$1_$ctor_D6A77C0(), Elements_Element$2__Run_706D0133(builder$0040_2, Elements_Element$2__Yield_Z5833CC7(builder$0040_2, (armsArm_S6n2_S5x3_S6x4) => {
        const armsRead_S6n2 = values(values(armsArm_S6n2_S5x3_S6x4, armsTag_S5)[3], armsTag_S6);
        return armsRead_S6n2[2];
    }))), (builder$0040_3 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_3, Elements_Element$2__Yield_Z721C83C5(builder$0040_3, "closed")))]]))]), (builder$0040_4 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_4, Elements_Element$2__Yield_Z721C83C5(builder$0040_4, "Other")))]];
})())]));

export const failing = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1;
    const armsTag_S7 = {};
    return [(armsInstance_S7, _arg_1) => {
        if (_arg_1.tag === 2) {
            toFail(printf("bad %s"))("state");
            return null;
        }
        else {
            return [0, armsTag_S7, _arg_1, armsInstance_S7];
        }
    }, [(builder$0040_1 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_1, Elements_Element$2__Yield_Z721C83C5(builder$0040_1, "Other")))]];
})())]));

const badge = (() => {
    const builder$0040 = Elements_i$1_$ctor_Z10189819();
    return Elements_Element$2__Run_706D0133(builder$0040, Elements_Element$2__Yield_Z5833CC7(builder$0040, (state) => toStringOrEmpty(Signal$1__get_Value(state))));
})();

export const spliced = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1;
    const armsTag_S8 = {};
    return [(armsInstance_S8, _arg_1) => ((_arg_1.tag === 2) ? [0, armsTag_S8, _arg_1.fields[0], _arg_1, armsInstance_S8] : [1, armsTag_S8, _arg_1, armsInstance_S8]), [(builder$0040_1 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_1, append(Elements_Element$2__Yield_Z5833CC7(builder$0040_1, (armsArm_S8n1_S8x4) => {
        const armsRead_S8n1 = values(armsArm_S8n1_S8x4, armsTag_S8);
        return armsRead_S8n1[2];
    }), [Arms_Match_7CDE3892((armsArm_S8n2_S8x4) => values(armsArm_S8n2_S8x4, armsTag_S8)[4], badge, null)]))), Arms_Match_7CDE3892((armsArm_S8n3_S8x3) => values(armsArm_S8n3_S8x3, armsTag_S8)[3], badge, null)]];
})())]));

export const locals = Builders_Component_template((_arg, state) => state, Elements_Element$2__Run_706D0133(Elements_div$1_$ctor_69400C3C(), [Arms_Match_7CDE3892(read, null, (() => {
    let builder$0040_1, builder$0040_2;
    const armsTag_S9 = {};
    return [(armsInstance_S9, _arg_1) => ((_arg_1.tag === 2) ? [0, armsTag_S9, _arg_1.fields[0], _arg_1, armsInstance_S9] : [1, armsTag_S9, _arg_1, armsInstance_S9]), [(builder$0040_1 = Elements_pre$1_$ctor_D6A77C0(), Elements_Element$2__Run_706D0133(builder$0040_1, Elements_Element$2__Yield_Z5833CC7(builder$0040_1, (armsArm_S9n2_S9x4) => {
        const armsRead_S9n1 = values(armsArm_S9n2_S9x4, armsTag_S9);
        const s = armsRead_S9n1[4];
        return armsRead_S9n1[2] + toStringOrEmpty(Signal$1__get_Value(s));
    }))), (builder$0040_2 = Elements_span$1_$ctor_Z7EAF50F5(), Elements_Element$2__Run_706D0133(builder$0040_2, Elements_Element$2__Yield_Z721C83C5(builder$0040_2, "Other")))]];
})())]));

//# sourceMappingURL=Sites.js.map
