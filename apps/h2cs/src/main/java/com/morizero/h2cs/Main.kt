package com.morizero.h2cs

import com.morizero.h2cs.api.Attribute
import com.morizero.h2cs.api.MilizeAPIInfo
import com.morizero.h2cs.api.Parameter
import com.morizero.h2cs.generated.parser.CPP14Lexer
import com.morizero.h2cs.generated.parser.CPP14Parser
import com.morizero.h2cs.generated.parser.CPP14ParserBaseVisitor
import org.antlr.v4.runtime.*
import org.antlr.v4.runtime.misc.Interval
import org.w3c.dom.Attr
import java.io.File


class H2CSVisitor(val input: CodePointCharStream) : CPP14ParserBaseVisitor<Unit>() {
    val apiList = mutableListOf<MilizeAPIInfo>()
    val frameworkStaticBinding = mutableListOf<String>()

    private fun parseAttribute(attribute: List<CPP14Parser.AttributeContext>): List<Attribute> {
        val attributes = mutableListOf<Attribute>()
        for (attr in attribute) {
            val item = Attribute()
            item.namespace = attr.attributeNamespace()?.text ?: ""
            item.name = attr.Identifier().text
            item.args =
                attr.attributeArgumentClause()?.balancedTokenSeq()?.balancedtoken()?.map { it.text } ?: listOf()
            attributes.add(item)
        }
        return attributes
    }

    override fun visitSimpleDeclaration(ctx: CPP14Parser.SimpleDeclarationContext) {
        val ret = MilizeAPIInfo()

        for (attrSpec in ctx.attributeSpecifierSeq()?.attributeSpecifier() ?: listOf()) {
            ret.attributes += parseAttribute(attrSpec.attributeList().attribute())
        }
        if (ret.attributes.any { it.namespace == "milize" && it.name == "CSharpIgnore" }) {
            return
        }

        val declSpecifierSeq = ctx.declSpecifierSeq()
        ret.modifier = declSpecifierSeq.declSpecifier().let { it.subList(0, it.size - 1).map { it.text }.toList() }
        ret.returnType = ctx.declSpecifierSeq().declSpecifier().last().text

        val noPointerDeclarator =
            ctx.initDeclaratorList().initDeclarator(0).declarator().pointerDeclarator().noPointerDeclarator()
        ret.functionName = noPointerDeclarator.noPointerDeclarator().text
        val parametersAndQualifiers = noPointerDeclarator.parametersAndQualifiers()
        val parameterDeclarationClause = parametersAndQualifiers.parameterDeclarationClause()
        ret.parameters = parameterDeclarationClause?.parameterDeclarationList()?.parameterDeclaration()?.map {
            val parameter = Parameter()

            val attributes = mutableListOf<Attribute>()
            for (attrSpec in it.attributeSpecifierSeq()?.attributeSpecifier() ?: listOf()) {
                attributes += parseAttribute(attrSpec.attributeList().attribute())
            }

            parameter.type = it.declSpecifierSeq().declSpecifier().map { it.text }
            parameter.attributes = attributes
            parameter.name = it.declarator().pointerDeclarator().noPointerDeclarator().text
            val pointerOperator = it.declarator().pointerDeclarator().text;
            parameter.isReference = pointerOperator.contains("&")
            parameter.isPointer = pointerOperator.contains("*")
            parameter
        } ?: listOf()

        apiList += ret


        val declSpecifier = declSpecifierSeq.declSpecifier()

        val funcDeclStart = ctx.start.startIndex
        val funcDeclStop = ctx.stop.stopIndex

        val declSpecifierSeqStop = ctx.stop.stopIndex

        val funcNameStart = noPointerDeclarator.noPointerDeclarator().start.startIndex
        val funcNameStop = noPointerDeclarator.noPointerDeclarator().stop.stopIndex

        val bindingDeclTypeInfo = declSpecifier.map {
            val start = it.start.startIndex
            val stop = it.stop.stopIndex
            input.getText(Interval(start, stop))
        }.filter { it != "MILESTRO_API" }.joinToString(separator = " ", prefix = "", postfix = " ")

        val functionName = input.getText(Interval(funcNameStart, funcNameStop));
        val bindingDeclFunctionName = "FrameworkBinding" + functionName;
        val declFunctionRestPart = input.getText(Interval(funcNameStop + 1, funcDeclStop - 1))
        val frameworkCall = "${functionName}${
            ret.parameters.map { it.name }.joinToString(separator = ", ", prefix = "(", postfix = ")")
        }"

        if (!ret.attributes.any { it.namespace == "milize" && it.name == "EditorOnly" }) {
            frameworkStaticBinding += """
            ${bindingDeclTypeInfo} ${bindingDeclFunctionName} ${declFunctionRestPart} {
                return ${frameworkCall};
            }
        """.trimIndent()
        }
    }

}

object Main {
    @JvmStatic
    fun main(args: Array<String>) {
        val file = File(args[0])
        val input = CharStreams.fromReader(file.reader())
        val lexer = CPP14Lexer(input)
        val tokens = CommonTokenStream(lexer)
        val parser = CPP14Parser(tokens)

        val tree = parser.translationUnit()

        val visitor = H2CSVisitor(input)
        visitor.visit(tree)

        visitor.writeCSharpBinding(args[1])
        visitor.writeFrameworkBinding(args[2])
    }
}

private fun H2CSVisitor.writeCSharpBinding(path: String) {
    val result =
        """using System;
using System.Runtime.InteropServices;

namespace Milestro.Binding
{
    public class BindingC
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string dllName = "__Internal";
        private const string EntryPointPrefix = "FrameworkBinding";
#else
        private const string dllName = "libMilestro";
        private const string EntryPointPrefix = "";
#endif

${apiList.map { it.toCS() }.joinToString("\n\n")}

    }
}
"""

    val outFile = File(path)
    outFile.printWriter().let {
        it.print(result)
        it.close()
    }
}

private fun H2CSVisitor.writeFrameworkBinding(path: String) {
    val result = """#include "Milestro/milestro_game_interface.h"

extern "C" {
${frameworkStaticBinding.joinToString("\n\n")}
}
"""

    val outFile = File(path)
    outFile.printWriter().let {
        it.print(result)
        it.close()
    }
}
