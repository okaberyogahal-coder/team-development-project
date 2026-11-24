import type {PublicLesson,PublicQuestion,QuizResult} from '../../shared/ai-instructor';

// Only import this module from server code. Public projections explicitly omit grading data.
const lesson:PublicLesson={
 id:'natural-light',version:1,title:'自然光と背景ぼけの基本',sample:true,
 goals:['窓辺の光の向きを工夫できる','背景をすっきり見せる方法を知る','身近な被写体で撮り方を比べられる'],
 sections:[
  {id:'window-light',title:'1. 窓辺のやわらかい光',body:'まずは花やカップなど、動かない被写体を窓辺に置いてみましょう。直射日光が強いと、影がくっきり出ることがあります。薄いカーテンを通した光なら、影がやわらかくなりやすくなります。被写体の横から光が当たる位置で撮り、次に光の向きを変えて、明るい部分と影を見比べてください。撮影中も太陽や強い光を直接見ないようにしましょう。'},
  {id:'background-distance',title:'2. 被写体と背景を離す',body:'背景ぼけとは、写したい被写体にピントが合い、背景がぼんやり写ることです。まず被写体の後ろにある背景を少し離して撮り比べます。カメラやレンズによっては、絞りを開く（F値を小さくする）と背景がぼけやすくなりますが、機種や撮影距離でも変わります。スマートフォンではポートレートモードが使える機種もあります。すべての機種で同じぼけ方になるわけではないので、被写体にピントを合わせたまま結果を確かめましょう。'},
  {id:'simple-background',title:'3. 背景を整えて撮り比べる',body:'背景がぼけなくても、写したいものを見やすくする工夫はできます。背景に物が多いときは、不要な物を片づけたり、壁などのすっきりした面を背景にしたりします。撮る位置を少し変えて、被写体の後ろに目立つ物が重ならないようにしてみましょう。最後に、同じ被写体で光の向きや背景を一つずつ変え、どの写真が見やすいか比べます。高価な機材を買わずに、手元の道具で試せます。'},
 ],
};
const questions=[
 {id:'light',prompt:'窓辺の光が強く、影がくっきり出るとき、やわらかい光を試す方法は？',choices:['薄いカーテンを通した光で撮る','必ず新しいカメラを買う','太陽を直接見て位置を確かめる'],answer:0,sectionId:'window-light',explanation:'薄いカーテンを通すと、光と影がやわらかくなりやすくなります。'},
 {id:'distance',prompt:'背景ぼけを試すための、教材で紹介した工夫は？',choices:['必ず被写体を壁に密着させる','被写体と背景を離して撮り比べる','すべての機種で同じ設定にする'],answer:1,sectionId:'background-distance',explanation:'被写体と背景を離して撮り比べます。ぼけ方は機種や距離でも変わります。'},
 {id:'background',prompt:'背景がぼけないとき、被写体を見やすくするには？',choices:['背景の物を増やす','必ず撮影をやめる','不要な物を片づけ、撮る位置を変える'],answer:2,sectionId:'simple-background',explanation:'背景を整理したり、撮る位置を変えたりすることで被写体が見やすくなります。'},
];
export function getPublicLesson(id:string):PublicLesson|null{return id===lesson.id?structuredClone(lesson):null;}
export function getPublicQuestions(id:string):PublicQuestion[]{
 return id===lesson.id?questions.map(({id,prompt,choices})=>({id,prompt,choices:[...choices]})):[];
}
export function gradeQuestion(materialId:string,questionId:string,choice:number):QuizResult{
 const q=materialId===lesson.id?questions.find(q=>q.id===questionId):undefined;
 if(!q||!Number.isInteger(choice)||choice<0||choice>=q.choices.length)throw new Error('INVALID');
 return {correct:choice===q.answer,explanation:q.explanation,sectionId:q.sectionId};
}
