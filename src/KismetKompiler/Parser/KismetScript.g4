grammar KismetScript;

//////////////////
//
// Parser rules
//
//////////////////

// Basic constructs
compilationUnit
	: importStatement* declarationStatement* EOF
	;

importStatement
	: Import '(' StringLiteral ')' ';'
	;

statement
	: nullStatement
	| compoundStatement
	| declarationStatement
	| expression ';'
	| ifStatement
	| forStatement
	| whileStatement
	| breakStatement
	| continueStatement
	| returnStatement
	| gotoStatement
	| switchStatement
	;

nullStatement
	: ';'
	;

compoundStatement
	: '{' statement* '}'
	;

//
// Declaration statements
//
declarationStatement
	: functionDeclarationStatement
	| procedureDeclarationStatement
	| variableDeclarationStatement
	| enumTypeDeclarationStatement
	| labelDeclarationStatement
	;

functionDeclarationStatement
	: Function'('IntLiteral')' typeIdentifier Identifier parameterList ';'
	;

procedureDeclarationStatement
	: attributeList? procedureModifier* typeIdentifier Identifier parameterList compoundStatement
	;

variableDeclarationStatement
	: attributeList? variableModifier? typeIdentifier Identifier ('=' expression)? ';'
	;

arraySignifier
	: ('['  ']')
	;

enumTypeDeclarationStatement
	: Enum Identifier enumValueList
	;

enumValueDeclaration
	: Identifier ( '=' expression )?
	;

enumValueList
	: '{' enumValueDeclaration? ( enumValueDeclaration ',' )* ( enumValueDeclaration ','? )? '}'
	;

attributeList
	: '[' Identifier (',' Identifier)* ']'
	;

labelDeclarationStatement
	: Identifier ':'
	;

procedureModifier
	: Public
	| Private
	| Protected
	| Sealed
	| Static
	| Virtual
	;

variableModifier
	: Global ('('IntLiteral')')?
	| Const
	| AiLocal ('('IntLiteral')')?
	| AiGlobal ('('IntLiteral')')?
	| Bit ('('IntLiteral')')
	| Count ('('IntLiteral')')?
	| Local ('('IntLiteral')')?
	;

//
// Parameters
//
parameterList
	: '(' parameter? (',' parameter)* ')'
	;

parameter
	: attributeList? parameterModifier? typeIdentifier Identifier arraySignifier?
	;

parameterModifier
	: Out
	| Ref
	| Const
	;

//
// Arguments
//
argumentList
	: '(' argument? (',' argument)* ')'
	;

argument
	: expression
	| Out Identifier
	;

//
// Expressions
//
expressionList
	: '(' (expression)? (',' expression)* ')'
	;

expression
	: ';'																	# nullExpression
	| '(' expression ')'													# compoundExpression
	| '{' (expression)? (',' expression)* (',')? '}'						# braceInitializerListExpression
	| '[' (expression)? (',' expression)* (',')? ']'						# bracketInitializerListExpression
	| Identifier '[' expression ']'											# subscriptExpression
	| expression Op=('.'|'->') expression									# memberExpression
	| '(' typeIdentifier ')' '(' expression ')'								# castExpression				// precedence 2
	| Identifier argumentList												# callExpression				// precedence 2
	| expression Op=( '--' | '++' )											# unaryPostfixExpression		// precedence 2
	| Op=( '!' | '-' | '--' | '++' ) expression								# unaryPrefixExpression			// precedence 3
	| expression Op=( '*' | '/' | '%' ) expression							# multiplicationExpression		// precedence 5
	| expression Op=( '+' | '-' ) expression								# additionExpression			// precedence 6
	| expression Op=( '<' | '>' | '<=' | '>=' ) expression					# relationalExpression			// precedence 8
	| expression Op=( '==' | '!=' ) expression								# equalityExpression			// precedence 9	
	| expression '&&' expression											# logicalAndExpression			// precedence 13
	| expression '||' expression											# logicalOrExpression			// precedence 14
	| expression Op=( '=' | '+=' | '-=' | '*=' | '/=' | '%=') expression	# assignmentExpression			// precedence 15
	| primary																# primaryExpression
	;

primary
	: constant		# constantExpression
	| Identifier	# identifierExpression
	;

constant
	: BoolLiteral
	| IntLiteral
	| FloatLiteral
	| StringLiteral
	;

//
// Flow control statements
//
ifStatement
	: If '(' expression ')' statement (Else statement)*
	;

// not perfect
forStatement
	: For '(' statement expression ';' expression ')' statement
	;

whileStatement
	: While expression statement
	;

breakStatement
	: Break ';'
	;

continueStatement
	: Continue ';'
	;

returnStatement
	: Return expression? ';'
	;

gotoStatement
	: Goto Identifier ';'
	| Goto Case expression ';'
	| Goto Case Default ';'
	;

switchStatement
	: Switch '(' expression ')' '{' switchLabel+ '}'
	;

switchLabel
	: Case expression ':' statement*
	| Default ':' statement*
	;

typeIdentifier
	: BuiltinTypeIdentifier arraySignifier?
	| Identifier arraySignifier?
	;

////////////////////
//
// Lexer rules
//
////////////////////

// Keywords
//	Directives
Import:		'import';

//	Storage types
Function:	'function';
Global:		'global';
Const:		'const';
AiLocal:	'ai_local';
AiGlobal:	'ai_global';
Bit:		'bit';
Enum:		'enum';
Out:		'out';
Local:		'local';
Count:		'count';

// Modifiers
Public:		'public';
Private:	'private';
Protected:	'protected';
Sealed:		'sealed';
Static:		'static';
Virtual:	'virtual';


//	Control flow
If:			'if';
Else:		'else';
For:		'for';
While:		'while';
Break:		'break';
Continue:	'continue';
Return:		'return';
Goto:		'goto';
Switch:		'switch';
Case:		'case';
Default:	'default';

// Literals

// Boolean constants
BoolLiteral
	: ( True | False )
	;

fragment
True:		'true';

fragment
False:		'false';

fragment
IdentifierEscape: '``';

// Integer constants
IntLiteral
	: ( DecIntLiteral | HexIntLiteral );

fragment
DecIntLiteral
	: Sign? Digit+;

fragment
HexIntLiteral
	: Sign? HexLiteralPrefix HexDigit+;

// Float constant
FloatLiteral
	: Sign? Digit* '.'? Digit+ ( FloatLiteralExponent Sign? Digit+ )? FloatLiteralSuffix?
	;

fragment
FloatLiteralSuffix
	: ( 'f' | 'F' )
	;

fragment
FloatLiteralExponent
	: ( 'e' | 'E' )
	;

// String constant
StringLiteral
	: '"' ( StringEscapeSequence | ~( '\\' | '"' ) )* '"'
	;

fragment
StringEscapeSequence
    : '\\' ( [abfnrtvz"'] | '\\' )
    | '\\' '\r'? '\n'
    | StringDecimalEscape
    | StringHexEscape
    ;
    
fragment
StringDecimalEscape
    : '\\' Digit
    | '\\' Digit Digit
    | '\\' [0-2] Digit Digit
    ;
    
fragment
StringHexEscape
    : '\\' 'x' HexDigit HexDigit;

// Identifiers
BuiltinTypeIdentifier
	: PrimitiveTypeIdentifier
	;
 
PrimitiveTypeIdentifier
	: 'bool'
	| 'byte'
	| 'int'
	| 'float'
	| 'string'
	| 'void'
	;

Identifier
	: ( Letter | '_' ) ( Letter | '_' | Digit )*		// C style identifier
	| IdentifierEscape ( ~( '`' ) )* IdentifierEscape	// Verbatim string identifier for otherwise invalid names
	;

fragment
Letter
	: ( [a-zA-Z] );

fragment
Digit
	: [0-9];

fragment
HexDigit
	: ( Digit | [a-fA-F] );

fragment
HexLiteralPrefix
	: '0' [xX];

fragment
Sign
	: '+' | '-';


// Whitespace, newline & comments
Whitespace
    :   [ \t]+
        -> skip
    ;

Newline
    :   (   '\r' '\n'?
        |   '\n'
        )
        -> skip
    ;

BlockComment
    :   '/*' .*? '*/'
        -> skip
    ;

LineComment
    :   '//' ~( '\r' | '\n' )*
        -> skip
    ;