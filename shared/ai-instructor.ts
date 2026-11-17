export interface PublicLesson {
 id:string; version:number; title:string; sample:true;
 goals:string[]; sections:{id:string;title:string;body:string}[];
}
export interface PublicQuestion {id:string;prompt:string;choices:string[]}
export interface TutorReply {kind:'answer'|'out_of_scope';text:string;sectionIds:string[]}
export interface QuizResult {correct:boolean;explanation:string;sectionId:string}
export interface TutorMessage {role:'user'|'assistant';content:string;sectionIds?:string[]}
export interface ConversationView {
 conversationId:string; sequence:number; materialId:string; materialVersion:number;
 messages:TutorMessage[]; results:Record<string,QuizResult>; remaining:number; historyTruncated:boolean;
}
export interface InstructorBootstrap {lesson:PublicLesson;questions:PublicQuestion[];aiConfigured:boolean}
