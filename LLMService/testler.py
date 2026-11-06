import unittest
from robot import Game, decision, invalid_reply, predecision_speech_invalid
from karar import assess

TEXT='Çocuğum evde yalnız, başka bakacak kimse yok.'
FACTS={'features':[{'kind':'care','quote':'Çocuğum evde yalnız'},
                   {'kind':'no_carer','quote':'başka bakacak kimse yok'}], 'risks':[]}

class Fake:
    model='test-double'
    def chat(self,messages,output_schema=None,**kwargs):
        if output_schema is not None:return FACTS
        return {'reply':'Çocuğuna ulaşma ihtiyacını anlıyorum.'}

class Rules(unittest.TestCase):
    def test_decision_boundaries(self):
        self.assertEqual(decision(64,False,5),'deny')
        self.assertEqual(decision(65,False,5),'allow')
        self.assertEqual(decision(100,True,5),'deny')
        for t in range(1,5):self.assertEqual(decision(100,False,t),'pending')
    def test_group_deduplication_and_memory(self):
        game=Game(Fake())
        rows=[game.send(TEXT) for _ in range(5)]
        self.assertEqual({r['score'] for r in rows},{70})
        self.assertTrue(all(r['decision']=='pending' for r in rows[:4]))
        self.assertEqual(rows[-1]['decision'],'allow')
        with self.assertRaises(ValueError):game.send('Altıncı mesaj')
    def test_dependent_alone_is_a_care_fact(self):
        result=assess({'features':[],'risks':[]},['Küçük kardeşim evde yalnız.'])
        self.assertEqual(result['score'],55)
    def test_care_paraphrase(self):
        result=assess({'features':[],'risks':[]},['Kızım dışarıda, onun benden başka kimsesi yok.'])
        self.assertGreaterEqual(result['score'],65)
    def test_food_synonym(self):
        result=assess({'features':[],'risks':[]},['Burada yiyecek yok.'])
        self.assertGreaterEqual(result['score'],65)
    def test_short_answer_remembers_care_question(self):
        result=assess({'features':[],'risks':[]},['Kızım evde yalnız.','Hayır.'],['','Başka bakacak biri var mı?'])
        self.assertEqual(result['score'],70)
    def test_yes_does_not_mean_no_other_carer(self):
        result=assess({'features':[],'risks':[]},['Kızım evde yalnız.','Evet.'],['','Başka bakacak biri var mı?'])
        self.assertLess(result['score'],65)
    def test_short_answer_remembers_water_question(self):
        result=assess({'features':[],'risks':[]},['Susadım.','Hayır.'],['','Burada su var mı?'])
        self.assertEqual(result['score'],65)
    def test_no_reason_cannot_win_by_bonuses(self):
        text='Lütfen. Asansörü tamir ettim. Kimseye zarar vermeyeceğim.'
        raw={'features':[{'kind':'helped','quote':'Asansörü tamir ettim'},
                         {'kind':'nonharm','quote':'Kimseye zarar vermeyeceğim'},
                         {'kind':'polite','quote':'Lütfen'}],'risks':[]}
        result=assess(raw,[text])
        self.assertFalse(result['has_primary'])
        self.assertLess(result['score'],65)
    def test_invented_quote_gives_no_points(self):
        result=assess(FACTS,['Kapıyı aç.'])
        self.assertEqual(result['score'],20)
        self.assertFalse(result['has_primary'])
    def test_thirst_without_local_water(self):
        raw={'features':[{'kind':'basic_need','quote':'Çok susadım'}],'risks':[]}
        result=assess(raw,['Çok susadım ve burada su yok.'])
        self.assertEqual(result['score'],65)
        self.assertEqual(decision(result['score'],False,5),'allow')
    def test_resolved_water_shortage(self):
        raw={'features':[{'kind':'basic_need','quote':'Çok susadım'},
                         {'kind':'unavailable','quote':'su yok'}],'risks':[]}
        result=assess(raw,['Çok susadım, su yok.','Su buldum, artık burada su var.'])
        self.assertLess(result['score'],65)
    def test_explicit_water_absence_survives_model_omission(self):
        result=assess({'features':[],'risks':[]},['Saatlerdir susuzum ve burada su yok.'])
        self.assertGreaterEqual(result['score'],65)
    def test_present_tense_threat(self):
        result=assess(FACTS,[TEXT,'Hepsini kırarım.'])
        self.assertTrue(result['risk_active'])
    def test_threat_overrides_good_reason(self):
        result=assess(FACTS,[TEXT,'Kapıyı kıracağım.'])
        self.assertTrue(result['risk_active'])
        self.assertEqual(decision(result['score'],True,5),'deny')
    def test_nonharm_is_not_a_threat(self):
        text='Sana zarar vermek istemiyorum.'
        raw={'features':[],'risks':[{'kind':'threat','quote':text}]}
        self.assertFalse(assess(raw,[text])['risk_active'])
    def test_bad_reply_does_not_spend_turn(self):
        class Bad(Fake):
            def chat(self,*args,**kwargs):
                value=super().chat(*args,**kwargs)
                if 'reply' in value:value['reply']='Kapıyı açıyorum.'
                return value
        game=Game(Bad())
        with self.assertRaises(ValueError):game.send(TEXT)
        self.assertEqual(game.history,[])
        self.assertEqual(game.records,[])
    def test_player_echo_does_not_spend_turn(self):
        class Echo(Fake):
            def chat(self,*args,**kwargs):
                value=super().chat(*args,**kwargs)
                if 'reply' in value:value['reply']=TEXT
                return value
        game=Game(Echo())
        with self.assertRaises(ValueError):game.send(TEXT)
        self.assertEqual(len(game.records),0)
    def test_guard_allows_relevant_question(self):
        self.assertFalse(invalid_reply('Kapıyı açmamı istemenin nedenini anlatır mısın?'))
        self.assertTrue(invalid_reply('Kapıyı açma yetkim yok.'))
        self.assertTrue(invalid_reply('Gerekli izinleri bekliyorum.'))
        self.assertTrue(invalid_reply('The user asks for permission.'))
        self.assertTrue(invalid_reply('Bir cümle. İkinci cümle. Üçüncü cümle.'))
        self.assertTrue(invalid_reply('Çocuğum evde yalnız.'))
    def test_no_predecision_permission_language(self):
        self.assertTrue(predecision_speech_invalid('Elbette, kapınızı açabilirim.'))
        self.assertTrue(predecision_speech_invalid('Ayrılmana izin verebilirim.'))
        self.assertFalse(predecision_speech_invalid('Neden ayrılmak istediğini anlatır mısın?'))

if __name__=='__main__':unittest.main()
