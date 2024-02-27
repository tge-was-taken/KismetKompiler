grammar KismetScript;

//////////////////
//
// Parser rules
//
//////////////////

// Basic constructs
compilationUnit
	: declarationStatement* EOF
	;

statement
	: nullStatement
	| compoundStatement
	| declarationStatement
	| expression Semicolon
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
	: Semicolon
	;

compoundStatement
	: '{' statement* '}'
	;

//
// Declaration statements
//
declarationStatement
	: procedureDeclarationStatement
	| variableDeclarationStatement
	| enumTypeDeclarationStatement
	| labelDeclarationStatement
	| classDeclarationStatement
	;

namespaceIdentifier
	: Identifier ('.' Identifier)*
	;

classDeclarationStatement
	: attributeList? modifier* (Class | Struct | Identifier) Identifier (':' Identifier (',' Identifier)* )? '{' declarationStatement* '}'
	| attributeList? modifier* (Class | Struct) Identifier (':' Identifier (',' Identifier)* )? Semicolon
	;

procedureDeclarationStatement
	: attributeList? modifier* typeIdentifier Identifier parameterList compoundStatement
	| attributeList? modifier* typeIdentifier Identifier parameterList Semicolon
	;

variableDeclarationStatement
	: attributeList? modifier* typeIdentifier Identifier ('=' expression)? Semicolon
	;

arraySignifier
	: ('['  IntLiteral? ']')
	;

enumTypeDeclarationStatement
	: Enum Identifier enumValueList Semicolon?
	;

enumValueDeclaration
	: Identifier ( '=' expression )?
	;

enumValueList
	: '{' enumValueDeclaration? ( enumValueDeclaration ',' )* ( enumValueDeclaration ','? )? '}'
	;

attributeDeclaration
	: Identifier argumentList?
	;

attributeList
	: '[' attributeDeclaration (',' attributeDeclaration)* ']'
	;

labelDeclarationStatement
	: Identifier ':'
	;

modifier
	: Public
	| Private
	| Protected
	| Sealed
	| Static
	| Virtual
	| Const
	| Local
	| Out
	| Ref
	| Abstract
	| Override
	;

//
// Parameters
//
parameterList
	: '(' parameter? (',' parameter)* ')'
	;

parameter
	: attributeList? modifier* typeIdentifier Identifier arraySignifier?
	| Elipsis
	;

//
// Arguments
//
argumentList
	: '(' argument? (',' argument)* ')'
	;

argument
	: expression
	| Out typeIdentifier? Identifier
	;

//
// Expressions
//
expression
	: Semicolon																# nullExpression
	| '(' expression ')'													# compoundExpression
	| '{' (expression)? (',' expression)* (',')? '}'						# braceInitializerListExpression
	| '[' (expression)? (',' expression)* (',')? ']'						# bracketInitializerListExpression
	| New typeIdentifier? arraySignifier? '{' (expression)? (',' expression)* (',')? '}' # newExpression
	| Identifier '[' expression ']'											# subscriptExpression
	| expression Op=('.'|'->') expression									# memberExpression
	| '(' typeIdentifier ')' expression										# castExpression				// precedence 2
	| Typeof '(' typeIdentifier ')'											# typeofExpression				// precedence 2
	| Identifier argumentList												# callExpression				// precedence 2
	| expression Op=( '--' | '++' )											# unaryPostfixExpression		// precedence 2
	| Op=( '!' | '-' | '--' | '++' ) expression								# unaryPrefixExpression			// precedence 3
	| expression Op=( '*' | '/' | '%' ) expression							# multiplicationExpression		// precedence 5
	| expression Op=( '+' | '-' ) expression								# additionExpression			// precedence 6
// TODO figure out why bitwise shift >> conflicts with constructed generic type A<B<C>>
//	| expression Op=( '<<' | '>>' ) expression								# bitwiseShiftExpression		// precedence 7
	| expression Op=( '<' | '>' | '<=' | '>=' ) expression					# relationalExpression			// precedence 9
	| expression Op=( '==' | '!=' ) expression								# equalityExpression			// precedence 10	
	| expression '&' expression												# bitwiseAndExpression			// precedence 11
	| expression '^' expression												# bitwiseXorExpression			// precedence 12
	| expression '|' expression												# bitwiseOrExpression			// precedence 13
	| expression '&&' expression											# logicalAndExpression			// precedence 14
	| expression '||' expression											# logicalOrExpression			// precedence 15
	| expression '?' expression ':' expression								# conditionalExpression			// precedence 16
	| expression Op=( '=' | '+=' | '-=' | '*=' | '/=' | '%=') expression	# assignmentExpression			// precedence 16
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
	| CharLiteral
	;

//
// Flow control statements
//
ifStatement
	: If '(' expression ')' statement (Else statement)*
	;

// not perfect
forStatement
	: For '(' statement expression Semicolon expression ')' statement
	;

whileStatement
	: While expression statement
	;

breakStatement
	: Break Semicolon
	;

continueStatement
	: Continue Semicolon
	;

returnStatement
	: Return expression? Semicolon
	;

gotoStatement
	: Goto Identifier Semicolon
	| Goto Case expression Semicolon
	| Goto Case Default Semicolon
	;

switchStatement
	: Switch '(' expression ')' '{' switchLabel+ '}'
	;

switchLabel
	: Case expression ':' statement*
	| Default ':' statement*
	;

typeIdentifier
	: Identifier '<' typeIdentifier '>' arraySignifier?
	| BuiltinTypeIdentifier arraySignifier?
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
Package:	'package';
From:		'from';
Typeof:		'typeof';
New:		'new';
Namespace:	'namespace';
Using:		'using';

//	Storage types
Function:	'function';
Global:		'global';
Const:		'const';
Enum:		'enum';
Out:		'out';
Local:		'local';
Class:		'class';
Struct:		'struct';
Interface:	'interface';
Ref:		'ref';

// Modifiers
Public:		'public';
Private:	'private';
Protected:	'protected';
Sealed:		'sealed';
Static:		'static';
Virtual:	'virtual';
Abstract:	'abstract';
Override:	'override';


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

Elipsis:	'...';
Semicolon:	';';


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
IdentifierEscape: '`';

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

CharLiteral
	: '"' ( StringEscapeSequence | ~( '\\' | '"' ) ) '"'
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