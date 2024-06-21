package com.morizero.h2cs

import com.morizero.h2cs.generated.parser.CPP14Parser.ParametersAndQualifiersContext
import org.antlr.v4.runtime.Parser
import org.antlr.v4.runtime.TokenStream

abstract class CPP14ParserBase protected constructor(input: TokenStream?) : Parser(input) {
    protected fun IsPureSpecifierAllowed(): Boolean {
        try {
            val x = this._ctx // memberDeclarator
            val c = x.getChild(0).getChild(0)
            val c2 = c.getChild(0)
            val p = c2.getChild(1) ?: return false
            return (p is ParametersAndQualifiersContext)
        } catch (e: Exception) {
        }
        return false
    }
}
