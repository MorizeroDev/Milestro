package com.morizero.h2cs.api

private fun List<String>?.typeToCS(): String {
    return mapOf(
        listOf("uint8_t") to "byte",

        listOf("uint32_t") to "uint",
        listOf("int32_t") to "int",

        listOf("uint16_t") to "ushort",
        listOf("int16_t") to "short",

        listOf("uint64_t") to "ulong",
        listOf("int64_t") to "long",

        listOf("size_t") to "ulong",

        listOf("bool") to "bool",
        listOf("float") to "float",
        listOf("double") to "double",

        listOf("milestro::skia::Canvas") to "IntPtr",
        listOf("milestro::skia::Typeface") to "IntPtr",
        listOf("milestro::skia::textlayout::Paragraph") to "IntPtr",
        listOf("milestro::skia::textlayout::ParagraphBuilder") to "IntPtr",
        listOf("milestro::skia::textlayout::ParagraphStyle") to "IntPtr",
        listOf("milestro::skia::textlayout::StrutStyle") to "IntPtr",
        listOf("milestro::skia::textlayout::TextStyle") to "IntPtr",
    )[this] ?: throw Exception("unknown type: ${this}")
}

private fun String.typeToCS(): String {
    return listOf(this).typeToCS()
}

class Attribute {
    var namespace: String = ""
    var name: String = ""
    var args: List<String> = listOf()
}

class Parameter {
    var isPointer: Boolean = false
    var isReference: Boolean = false
    var type: List<String> = listOf()
    var name: String = ""
    var attributes: List<Attribute> = listOf()

    fun toCS(): String {
        val modifier = if (isReference) {
            "out"
        } else {
            ""
        }

        val printedType = attributes.firstOrNull { it.namespace == "milize" && it.name == "CSharpType" }.let {
            if (it == null) {
                var t = type.typeToCS()
                if (t == "IntPtr") {
                    if (!isPointer) {
                        throw Exception("complex structure but no pointer declaration")
                    }
                } else {
                    if (isPointer) {
                        t = "$t[]"
                    }
                }
                t
            } else {
                it.args[0].let { it.substring(1, it.length - 1) }
            }
        }

        return "${modifier} ${printedType} ${name}"
    }
}

class MilizeAPIInfo {
    var modifier: List<String> = listOf()
    var returnType: String = ""
    var functionName: String = ""
    var parameters: List<Parameter> = listOf()
    var attributes: List<Attribute> = listOf()

    fun toCS(): String {
        val cSymbolName = functionName!!
        val methodName = if (cSymbolName.startsWith("Milestro")) {
            cSymbolName.substring(8)
        } else {
            cSymbolName
        }

        val parameterList = parameters.map { it.toCS() }.joinToString(",\n")

        val macro = run {
            if (attributes.any { it.namespace == "milize" && it.name == "EditorOnly" }) {
                "#if UNITY_EDITOR || MILTHM_EDITOR" to "#endif"
            } else if (attributes.any { it.namespace == "milize" && it.name == "GameOnly" }) {
                "#if !(UNITY_EDITOR || MILTHM_EDITOR)" to "#endif"
            } else {
                "" to ""
            }
        }

        return """
            ${macro.first}
            [DllImport(dllName, EntryPoint = EntryPointPrefix + "${cSymbolName}")]
            internal static extern unsafe ${returnType.typeToCS()} ${methodName}(${parameterList});
            ${macro.second}
""".trimIndent()
    }
}
